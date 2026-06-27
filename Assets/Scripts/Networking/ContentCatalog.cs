using System.Collections.Generic;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Distributes DB-backed content to clients (M2.2, decision D1). Clients must not touch Postgres, so
/// the server serializes the content it loaded at startup and sends it to each client over Mirror on
/// ready; the client rebuilds its registries from the message. Built generically (one message per
/// content type) so every later type that clients need to see — abilities, races, … — reuses this
/// shape rather than reinventing it.
/// </summary>
public static class ContentCatalog
{
    /// <summary>One content type's catalog as JSON. 2.2 carries items; later types add their own message/field.</summary>
    public struct ItemCatalogMessage : NetworkMessage
    {
        public string itemsJson;
    }

    /// <summary>Server: build the item catalog message from what <c>ContentLoader</c> loaded at startup.</summary>
    public static ItemCatalogMessage BuildItems()
        => new ItemCatalogMessage { itemsJson = JsonConvert.SerializeObject(ContentLoader.Items) };

    /// <summary>Client: rebuild the item registry from a received catalog. No-op on the host (server already loaded it).</summary>
    public static void ApplyItems(ItemCatalogMessage msg)
    {
        if (NetworkServer.active) return; // host shares the server-populated registry
        if (ItemRegistry.Instance == null)
        {
            Debug.LogWarning("[Content] Item catalog arrived but ItemRegistry.Instance is null on the client.");
            return;
        }
        var items = JsonConvert.DeserializeObject<List<ItemSnapshot>>(msg.itemsJson) ?? new List<ItemSnapshot>();
        ItemRegistry.Instance.LoadFrom(items);
        Debug.Log($"[Content] Client received {items.Count} item(s) from the server catalog.");
    }
}
