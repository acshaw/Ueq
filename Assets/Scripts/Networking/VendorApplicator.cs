using Mirror;
using UnityEngine;

// Add to any NPC prefab that can be a vendor. Config is driven by the MobDefinition
// via MobApplicator — set vendorInventory and vendorOpenKeyword there, not here.
public class VendorApplicator : NetworkBehaviour, IOnConversationKeyword, IOnConversationEnd
{
    VendorInventory _vendorInventory;
    string          _openKeyword = "wares";

    [SyncVar] string _vendorId;

    public override void OnStartServer()
    {
        var mob = GetComponent<MobApplicator>();
        if (mob?.Definition != null)
        {
            _vendorInventory = mob.Definition.vendorInventory;
            _openKeyword     = mob.Definition.vendorOpenKeyword;
        }
        _vendorId = _vendorInventory != null ? _vendorInventory.name : "";
    }

    public bool HasItem(string itemId)
    {
        if (_vendorInventory == null) return false;
        foreach (var e in _vendorInventory.Entries)
            if (e.item != null && e.item.itemId == itemId) return true;
        return false;
    }

    void IOnConversationKeyword.OnConversationKeyword(NetworkIdentity player, string keyword)
    {
        if (!string.Equals(keyword, _openKeyword, System.StringComparison.OrdinalIgnoreCase)) return;
        var conn = player?.GetComponent<NetworkedPlayer>()?.connectionToClient;
        if (conn != null) TargetRpcOpenShop(conn);
    }

    void IOnConversationEnd.OnConversationEnd(NetworkIdentity player)
    {
        var conn = player?.GetComponent<NetworkedPlayer>()?.connectionToClient;
        if (conn != null) TargetRpcCloseShop(conn);
    }

    [TargetRpc]
    void TargetRpcOpenShop(NetworkConnection conn)
        => VendorUI.Instance?.Open(netIdentity, _vendorId);

    [TargetRpc]
    void TargetRpcCloseShop(NetworkConnection conn)
        => VendorUI.Instance?.Close();
}
