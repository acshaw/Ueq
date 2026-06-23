using Mirror;
using UnityEngine;

public class PlayerEquipment : NetworkBehaviour
{
    public const int SlotCount = EquipSlotUtil.Count;

    readonly SyncList<string> _slots = new();

    CharacterStats _stats;
    Health         _health;
    PlayerMana     _mana;

    public SyncList<string> Slots => _slots;

    void Awake()
    {
        _stats  = GetComponent<CharacterStats>();
        _health = GetComponent<Health>();
        _mana   = GetComponent<PlayerMana>();
    }

    public override void OnStartServer()
    {
        for (int i = 0; i < SlotCount; i++)
            _slots.Add("");
    }

    public string GetItemId(EquipSlot slot)
        => (int)slot < _slots.Count ? _slots[(int)slot] : "";

    public ItemDefinition GetItem(EquipSlot slot)
        => ItemRegistry.Instance?.Get(GetItemId(slot));

    public ItemDefinition GetWeapon() => GetItem(EquipSlot.Weapon);

    [Server]
    public bool TryEquip(EquipSlot slot, string itemId, PlayerInventory inv)
    {
        var def = ItemRegistry.Instance?.Get(itemId);
        if (def == null || !def.isEquippable || def.equipSlot != slot) return false;

        string current = _slots[(int)slot];

        // Remove the new item from inventory first — frees a slot for the possible swap
        if (!inv.RemoveItem(itemId)) return false;

        // Return currently-equipped item to inventory
        if (!string.IsNullOrEmpty(current))
        {
            if (!inv.AddItem(current))
            {
                inv.AddItem(itemId); // refund
                return false;
            }
            ApplyBonus(current, remove: true);
        }

        _slots[(int)slot] = itemId;
        ApplyBonus(itemId, remove: false);
        _health?.RefreshMax();
        _mana?.RefreshMax();
        return true;
    }

    [Server]
    public bool TryUnequip(EquipSlot slot, PlayerInventory inv)
    {
        string itemId = GetItemId(slot);
        if (string.IsNullOrEmpty(itemId)) return false;
        if (!inv.AddItem(itemId)) return false;

        ApplyBonus(itemId, remove: true);
        _slots[(int)slot] = "";
        _health?.RefreshMax();
        _mana?.RefreshMax();
        return true;
    }

    // ── Persistence (1.3) ───────────────────────────────────────────────────────

    /// <summary>Restore equipped items from a loaded snapshot and re-apply each item's stat bonus
    /// exactly once, then refresh HP/mana maxes. Must run AFTER race/class base stats are set
    /// (PlayerExperience.LoadState) and is the only bonus application on load — no double-count.</summary>
    [Server]
    public void LoadState(string[] slots)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            string id = (slots != null && i < slots.Length) ? (slots[i] ?? "") : "";
            _slots[i] = id;
            if (!string.IsNullOrEmpty(id))
                ApplyBonus(id, remove: false);
        }
        _health?.RefreshMax();
        _mana?.RefreshMax();
    }

    /// <summary>Export equipped item ids as plain data for a snapshot.</summary>
    public string[] ExportSlots()
    {
        var arr = new string[_slots.Count];
        for (int i = 0; i < _slots.Count; i++)
            arr[i] = _slots[i] ?? "";
        return arr;
    }

    void ApplyBonus(string itemId, bool remove)
    {
        if (_stats == null) return;
        var def = ItemRegistry.Instance?.Get(itemId);
        if (def == null) return;
        if (remove)
            _stats.RemoveEquipmentBonus(def.bonusStr, def.bonusSta, def.bonusAgi, def.bonusDex, def.bonusInt, def.bonusWis, def.bonusCha);
        else
            _stats.AddEquipmentBonus(def.bonusStr, def.bonusSta, def.bonusAgi, def.bonusDex, def.bonusInt, def.bonusWis, def.bonusCha);
    }
}
