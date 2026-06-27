using System.Collections.Generic;

/// <summary>
/// Server-only lookup of vendor inventories by id (M2.3). Unlike <c>ItemRegistry</c> this needs no
/// scene object and no client sync — vendor stock is read on the server (shop validation) and the
/// momentary stock list is pushed to a client only when its shop opens (devplan DC3). Populated by
/// <c>ContentLoader</c> at host start.
/// </summary>
public static class VendorRegistry
{
    static readonly Dictionary<string, VendorInventorySnapshot> _byId = new();

    public static void LoadFrom(IEnumerable<VendorInventorySnapshot> snapshots)
    {
        _byId.Clear();
        foreach (var s in snapshots)
            if (!string.IsNullOrEmpty(s.VendorId))
                _byId[s.VendorId] = s;
    }

    public static bool TryGet(string vendorId, out VendorInventorySnapshot snapshot)
        => _byId.TryGetValue(vendorId ?? "", out snapshot);

    /// <summary>The item ids this vendor sells (empty if unknown vendor).</summary>
    public static List<string> GetItemIds(string vendorId)
        => _byId.TryGetValue(vendorId ?? "", out var s) ? s.ItemIds : new List<string>();

    public static bool Sells(string vendorId, string itemId)
        => _byId.TryGetValue(vendorId ?? "", out var s) && s.ItemIds.Contains(itemId);
}
