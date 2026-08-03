using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerCorpse : NetworkBehaviour
{
    [SyncVar] bool            _isActive;
    [SyncVar] NetworkIdentity _owner;
    [SyncVar] int             _copper;
    [SyncVar] int             _xpDebt;

    readonly SyncList<InventorySlot> _loot = new();

    // Temp storage — populated before spawn, consumed in OnStartServer
    List<InventorySlot> _pendingItems;

    public SyncList<InventorySlot> Slots    => _loot;
    public int                     Copper   => _copper;
    public bool                    IsActive => _isActive;
    public NetworkIdentity         Owner    => _owner;

    // Call before NetworkServer.Spawn — sets SyncVars and stashes items for OnStartServer
    public void Prepare(NetworkIdentity owner, List<InventorySlot> items, int copper, int xpDebt, string playerName)
    {
        _owner         = owner;
        _copper        = copper;
        _xpDebt        = xpDebt;
        _isActive      = true;
        _pendingItems  = items;
        GetComponent<Nameplate>()?.SetLabel($"{playerName}'s Corpse");
    }

    // Fires during NetworkServer.Spawn — isServer is guaranteed true here
    public override void OnStartServer()
    {
        if (_pendingItems != null)
        {
            foreach (var slot in _pendingItems)
                if (!slot.IsEmpty) _loot.Add(slot);
            _pendingItems = null;
        }
        RpcApplyCorpseVisual();
    }

    // ── Loot API (called from NetworkedPlayer commands) ───────────────────────

    [Server]
    public InventorySlot PeekSlot(int index)
        => (index >= 0 && index < _loot.Count) ? _loot[index] : InventorySlot.Empty;

    [Server]
    public void RemoveSlot(int index)
    {
        if (index >= 0 && index < _loot.Count)
            _loot.RemoveAt(index);
        CheckEmpty();
    }

    [Server]
    public int TakeCopper()
    {
        int c = _copper;
        _copper = 0;
        CheckEmpty();
        return c;
    }

    // Returns the count of items that could NOT be taken (inventory full or an already-held LORE item) so
    // the caller (NetworkedPlayer.CmdTakeLootAll) can tell the player why the corpse isn't fully empty,
    // instead of the take silently doing nothing they can see.
    [Server]
    public int TakeAll(PlayerInventory inv)
    {
        int blocked = 0;
        for (int i = _loot.Count - 1; i >= 0; i--)
        {
            // 3.2.1: enforce LORE — a dupe of a held LORE item just stays on the corpse.
            if (inv.AddItem(_loot[i].itemId, _loot[i].quantity, enforceLore: true))
                _loot.RemoveAt(i);
            else
                blocked++;
        }
        if (_copper > 0)
        {
            inv.AddCurrency(_copper);
            _copper = 0;
        }
        CheckEmpty();
        return blocked;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    [Server]
    void CheckEmpty()
    {
        if (_loot.Count > 0 || _copper > 0) return;

        if (_xpDebt > 0 && _owner != null)
        {
            int recovered = _xpDebt / 2;
            _owner.GetComponent<PlayerExperience>()?.AddXp(recovered);
            ChatManager.Instance?.SendDirect(
                new ChatMessage(ChatChannel.Reward, "System",
                    $"You recover your corpse and regain {recovered} experience."),
                _owner.connectionToClient);
        }

        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    void RpcApplyCorpseVisual()
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", new Color(0.25f, 0.20f, 0.35f));
            r.SetPropertyBlock(mpb);
        }
    }
}
