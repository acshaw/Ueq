using Mirror;
using UnityEngine;

// Add to any NPC prefab that can be a vendor. Config is driven by the MobDefinition via MobApplicator —
// set vendorId (a vendor_inventories row, M2.3) and vendorOpenKeyword there, not here. The stock list
// lives in the DB (VendorRegistry, server-only); it is pushed to the player's client on shop-open.
public class VendorApplicator : NetworkBehaviour, IOnConversationKeyword, IOnConversationEnd
{
    string _vendorId = "";
    string _openKeyword = "wares";

    public override void OnStartServer()
    {
        var mob = GetComponent<MobApplicator>();
        if (mob?.Definition != null)
        {
            _vendorId    = mob.Definition.vendorId;
            _openKeyword = mob.Definition.vendorOpenKeyword;
        }
    }

    /// <summary>Server-side check that this vendor sells the item (used by CmdBuyItem).</summary>
    public bool HasItem(string itemId) => VendorRegistry.Sells(_vendorId, itemId);

    void IOnConversationKeyword.OnConversationKeyword(NetworkIdentity player, string keyword)
    {
        if (!string.Equals(keyword, _openKeyword, System.StringComparison.OrdinalIgnoreCase)) return;
        var conn = player?.GetComponent<NetworkedPlayer>()?.connectionToClient;
        if (conn != null)
            // Push the stock list to the client on open (DC3) — clients have no DB access.
            TargetRpcOpenShop(conn, VendorRegistry.GetItemIds(_vendorId).ToArray());
    }

    void IOnConversationEnd.OnConversationEnd(NetworkIdentity player)
    {
        var conn = player?.GetComponent<NetworkedPlayer>()?.connectionToClient;
        if (conn != null) TargetRpcCloseShop(conn);
    }

    [TargetRpc]
    void TargetRpcOpenShop(NetworkConnection conn, string[] itemIds)
        => VendorUI.Instance?.Open(netIdentity, itemIds);

    [TargetRpc]
    void TargetRpcCloseShop(NetworkConnection conn)
        => VendorUI.Instance?.Close();
}
