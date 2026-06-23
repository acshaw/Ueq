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

    [Server]
    public bool AddItem(string itemId, int quantity = 1)
    {
        if (string.IsNullOrEmpty(itemId) || quantity <= 0) return false;

        var def = ItemRegistry.Instance?.Get(itemId);
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
