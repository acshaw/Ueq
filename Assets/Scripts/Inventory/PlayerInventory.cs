using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    public const int SlotCount = 8;

    readonly SyncList<InventorySlot> _slots = new();

    [SyncVar] int _copper;
    [SyncVar] int _silver;
    [SyncVar] int _gold;
    [SyncVar] int _platinum;

    // ── Public read ───────────────────────────────────────────────────────────

    public SyncList<InventorySlot> Slots    => _slots;
    public int Copper   => _copper;
    public int Silver   => _silver;
    public int Gold     => _gold;
    public int Platinum => _platinum;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void OnStartServer()
    {
        for (int i = 0; i < SlotCount; i++)
            _slots.Add(InventorySlot.Empty);
    }

    // ── Item API (server-only) ────────────────────────────────────────────────

    // 3.2.1: <paramref name="enforceLore"/> is opt-in and defaults false so INTERNAL moves (equip/unequip/swap,
    // which add an item the player already owns) are never blocked — TryUnequip adds while the item is still in
    // the equip slot, so an unconditional LORE block would trap it. EXTERNAL acquires (loot / vendor buy / quest
    // reward) pass true. A LORE item is capped at one in possession (inventory + equipped), regardless of stack.
    [Server]
    public bool AddItem(string itemId, int quantity = 1, bool enforceLore = false)
    {
        if (string.IsNullOrEmpty(itemId) || quantity <= 0) return false;

        var def = ItemRegistry.Instance?.Get(itemId);

        if (enforceLore && def != null && def.lore)
        {
            if (AlreadyHolds(itemId)) return false; // already have the one allowed copy
            quantity = 1;                           // LORE = max one, overriding maxStackSize
        }

        int maxStack = def != null ? def.maxStackSize : 1;

        int remaining = quantity;

        // Fill existing stacks first
        if (maxStack > 1)
        {
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var s = _slots[i];
                if (s.itemId == itemId && s.quantity < maxStack)
                {
                    int add = Mathf.Min(remaining, maxStack - s.quantity);
                    _slots[i] = new InventorySlot { itemId = itemId, quantity = s.quantity + add };
                    remaining -= add;
                }
            }
        }

        // Fill empty slots
        for (int i = 0; i < _slots.Count && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty)
            {
                int add = Mathf.Min(remaining, maxStack);
                _slots[i] = new InventorySlot { itemId = itemId, quantity = add };
                remaining -= add;
            }
        }

        return remaining == 0;
    }

    [Server]
    public bool RemoveItem(string itemId, int quantity = 1)
    {
        if (!HasItem(itemId, quantity)) return false;

        int remaining = quantity;
        for (int i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var s = _slots[i];
            if (s.itemId != itemId) continue;

            int take = Mathf.Min(remaining, s.quantity);
            int left = s.quantity - take;
            _slots[i] = left > 0
                ? new InventorySlot { itemId = itemId, quantity = left }
                : InventorySlot.Empty;
            remaining -= take;
        }
        return true;
    }

    public bool HasItem(string itemId, int quantity = 1)
    {
        int total = 0;
        foreach (var s in _slots)
            if (s.itemId == itemId) total += s.quantity;
        return total >= quantity;
    }

    // ── 3.2.1 LORE acquire guard ──────────────────────────────────────────────

    /// <summary>Does the player already hold this item anywhere — inventory OR an equipment slot?
    /// (The possession test for the LORE "max one" rule.)</summary>
    public bool AlreadyHolds(string itemId)
    {
        if (HasItem(itemId, 1)) return true;
        var equip = GetComponent<PlayerEquipment>();
        if (equip != null)
            foreach (var slot in equip.Slots)
                if (slot == itemId) return true;
        return false;
    }

    /// <summary>Pre-check for external acquire paths that consume the source before adding (e.g. vendor buy):
    /// true only if a LORE item isn't already held AND there's room. Lets a path refuse (and not charge/consume)
    /// before calling <see cref="AddItem"/> with enforceLore.</summary>
    public bool CanAcquire(string itemId, int quantity = 1)
    {
        if (string.IsNullOrEmpty(itemId) || quantity <= 0) return false;
        var def = ItemRegistry.Instance?.Get(itemId);
        if (def != null && def.lore)
        {
            if (AlreadyHolds(itemId)) return false;
            quantity = 1; // LORE = one
        }
        return RemainingCapacityFor(itemId, def) >= quantity;
    }

    /// <summary>3.2: can the player hand over <paramref name="take"/> and receive <paramref name="give"/> in one
    /// transaction? Simulates on a copy of the slots so a full bag can still turn-in-then-receive (the handed-over
    /// items free room for the reward). Returns false if a required item is missing, a LORE reward is already held,
    /// or the reward won't fit after the hand-off. Mutates nothing.</summary>
    public bool CanExchange(List<KeywordItemAmount> take, List<KeywordItemAmount> give)
    {
        var work = new List<InventorySlot>(_slots.Count);
        foreach (var s in _slots) work.Add(s);

        if (take != null)
            foreach (var t in take)
                if (t.quantity > 0 && !SimRemove(work, t.itemId, t.quantity)) return false;

        if (give != null)
            foreach (var g in give)
            {
                if (g.quantity <= 0) continue;
                var def = ItemRegistry.Instance?.Get(g.itemId);
                int qty = g.quantity;
                if (def != null && def.lore)
                {
                    if (AlreadyHolds(g.itemId)) return false; // can't grant a second LORE copy
                    qty = 1;
                }
                if (!SimAdd(work, g.itemId, def, qty)) return false;
            }

        return true;
    }

    static bool SimRemove(List<InventorySlot> slots, string itemId, int qty)
    {
        int total = 0;
        foreach (var s in slots) if (s.itemId == itemId) total += s.quantity;
        if (total < qty) return false;

        int remaining = qty;
        for (int i = slots.Count - 1; i >= 0 && remaining > 0; i--)
        {
            if (slots[i].itemId != itemId) continue;
            int take = Mathf.Min(remaining, slots[i].quantity);
            int left = slots[i].quantity - take;
            slots[i] = left > 0 ? new InventorySlot { itemId = itemId, quantity = left } : InventorySlot.Empty;
            remaining -= take;
        }
        return true;
    }

    static bool SimAdd(List<InventorySlot> slots, string itemId, ItemDefinition def, int qty)
    {
        int maxStack = def != null ? def.maxStackSize : 1;
        if (def != null && def.lore) maxStack = 1;

        int remaining = qty;
        if (maxStack > 1)
            for (int i = 0; i < slots.Count && remaining > 0; i++)
                if (slots[i].itemId == itemId && slots[i].quantity < maxStack)
                {
                    int add = Mathf.Min(remaining, maxStack - slots[i].quantity);
                    slots[i] = new InventorySlot { itemId = itemId, quantity = slots[i].quantity + add };
                    remaining -= add;
                }
        for (int i = 0; i < slots.Count && remaining > 0; i++)
            if (slots[i].IsEmpty)
            {
                int add = Mathf.Min(remaining, maxStack);
                slots[i] = new InventorySlot { itemId = itemId, quantity = add };
                remaining -= add;
            }
        return remaining == 0;
    }

    // How many more of this item will fit (existing-stack room + empty slots × stack size). LORE caps stack to 1.
    int RemainingCapacityFor(string itemId, ItemDefinition def)
    {
        int maxStack = def != null ? def.maxStackSize : 1;
        if (def != null && def.lore) maxStack = 1;

        int capacity = 0;
        foreach (var s in _slots)
        {
            if (s.IsEmpty) capacity += maxStack;
            else if (maxStack > 1 && s.itemId == itemId && s.quantity < maxStack) capacity += maxStack - s.quantity;
        }
        return capacity;
    }

    public bool IsFull()
    {
        foreach (var s in _slots)
            if (s.IsEmpty) return false;
        return true;
    }

    [Server]
    public void MoveSlot(int from, int to)
    {
        if ((uint)from >= (uint)_slots.Count || (uint)to >= (uint)_slots.Count) return;
        var tmp  = _slots[from];
        _slots[from] = _slots[to];
        _slots[to]   = tmp;
    }

    [Server]
    public void DropItem(int slotIndex)
    {
        if ((uint)slotIndex >= (uint)_slots.Count) return;
        _slots[slotIndex] = InventorySlot.Empty;
    }

    // ── Currency API (server-only) ────────────────────────────────────────────

    [Server]
    public void AddCurrency(int cp = 0, int sp = 0, int gp = 0, int pp = 0)
    {
        _copper   += cp;
        _silver   += sp;
        _gold     += gp;
        _platinum += pp;
        Normalize();
    }

    // Returns false if insufficient funds.
    [Server]
    public bool SpendCurrency(int cp = 0, int sp = 0, int gp = 0, int pp = 0)
    {
        int totalCp = TotalInCopper();
        int costCp  = cp + sp * 10 + gp * 100 + pp * 1000;
        if (totalCp < costCp) return false;

        totalCp -= costCp;
        SetFromCopper(totalCp);
        return true;
    }

    // ── Bulk API (server-only) ────────────────────────────────────────────────

    [Server]
    public void ClearAll()
    {
        for (int i = 0; i < _slots.Count; i++)
            _slots[i] = InventorySlot.Empty;
        _copper = _silver = _gold = _platinum = 0;
    }

    // ── Persistence (1.3) ───────────────────────────────────────────────────────

    /// <summary>Overwrite all slots + currency from a loaded snapshot. Slots are already created in
    /// OnStartServer, so this overwrites indices rather than adding.</summary>
    [Server]
    public void LoadState(InvEntry[] slots, int cp, int sp, int gp, int pp)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (slots != null && i < slots.Length && !string.IsNullOrEmpty(slots[i].Id) && slots[i].Q > 0)
                _slots[i] = new InventorySlot { itemId = slots[i].Id, quantity = slots[i].Q };
            else
                _slots[i] = InventorySlot.Empty;
        }
        _copper   = cp;
        _silver   = sp;
        _gold     = gp;
        _platinum = pp;
    }

    /// <summary>Export slots as plain data for a snapshot (main-thread capture).</summary>
    public InvEntry[] ExportSlots()
    {
        var arr = new InvEntry[_slots.Count];
        for (int i = 0; i < _slots.Count; i++)
            arr[i] = new InvEntry { Id = _slots[i].itemId ?? "", Q = _slots[i].quantity };
        return arr;
    }

    // ── Currency helpers ──────────────────────────────────────────────────────

    public int TotalCopperValue => _copper + _silver * 10 + _gold * 100 + _platinum * 1000;

    int TotalInCopper() => _copper + _silver * 10 + _gold * 100 + _platinum * 1000;

    void SetFromCopper(int total)
    {
        _platinum = total / 1000; total %= 1000;
        _gold     = total / 100;  total %= 100;
        _silver   = total / 10;   total %= 10;
        _copper   = total;
    }

    void Normalize() => SetFromCopper(TotalInCopper());
}
