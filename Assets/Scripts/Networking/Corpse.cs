using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class Corpse : NetworkBehaviour, IOnDeath
{
    [SerializeField] float despawnDelay = 300f;

    [SyncVar] bool _isActive;

    readonly SyncList<InventorySlot> _loot = new();
    [SyncVar] int _copper;

    public SyncList<InventorySlot> Slots    => _loot;
    public int                     Copper   => _copper;
    public bool                    IsActive => _isActive;

    // 5.3 (GP5) — the first ownership restriction on mob-corpse looting; previously any player in range
    // could loot any mob corpse. Snapshotted at death by MobKillReward (not a live party reference — a
    // later party change doesn't retroactively grant/revoke rights on an already-dead corpse). Server-only;
    // no client sync needed, only NetworkedPlayer's own loot commands read it, server-side.
    readonly HashSet<NetworkIdentity> _eligibleLooters = new();
    bool _eligibilitySet;

    [Server]
    public void SetEligibleLooters(List<NetworkIdentity> members)
    {
        _eligibilitySet = true;
        _eligibleLooters.Clear();
        if (members != null)
            foreach (var m in members)
                if (m != null) _eligibleLooters.Add(m);
    }

    // Falls back to "anyone" if eligibility was never resolved (e.g. a mob with no MobKillReward/
    // EnemyAI) — preserves today's open-looting behavior for that edge case instead of soft-locking it.
    [Server]
    public bool CanLoot(NetworkIdentity player)
        => !_eligibilitySet || (player != null && _eligibleLooters.Contains(player));

    // ── IOnDeath — dispatched by NpcEventDispatcher when Health reaches zero ──

    public void OnDeath(NetworkIdentity attacker)
    {
        if (!isServer) return;
        var mob = GetComponent<MobApplicator>()?.Definition;
        Activate(mob?.lootTable, mob?.displayName ?? gameObject.name);
    }

    // ── Server activation ─────────────────────────────────────────────────────

    [Server]
    void Activate(LootTable table, string mobName)
    {
        List<InventorySlot> slots;
        int copper;

        if (table != null)
            table.Roll(out slots, out copper);
        else
        {
            slots  = new List<InventorySlot>();
            copper = 0;
        }

        foreach (var slot in slots)
            _loot.Add(slot);

        _copper   = copper;
        _isActive = true;

        GetComponent<Nameplate>()?.SetLabel($"{mobName}'s Corpse");
        RpcApplyCorpseVisual();
        StartCoroutine(DespawnTimer());
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

    [Server]
    public void TakeAll(PlayerInventory inv)
    {
        for (int i = _loot.Count - 1; i >= 0; i--)
        {
            // 3.2.1: enforce LORE — a dupe of a held LORE item just stays on the corpse.
            if (inv.AddItem(_loot[i].itemId, _loot[i].quantity, enforceLore: true))
                _loot.RemoveAt(i);
        }
        if (_copper > 0)
        {
            inv.AddCurrency(_copper);
            _copper = 0;
        }
        CheckEmpty();
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    [Server]
    void CheckEmpty()
    {
        if (_loot.Count == 0 && _copper == 0)
            NetworkServer.Destroy(gameObject);
    }

    IEnumerator DespawnTimer()
    {
        yield return new WaitForSeconds(despawnDelay);
        if (isServer) NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    void RpcApplyCorpseVisual()
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", new Color(0.28f, 0.22f, 0.22f, 1f));
            r.SetPropertyBlock(mpb);
        }
    }
}
