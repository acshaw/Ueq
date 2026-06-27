using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime lookup of item definitions by <c>itemId</c>. Source of truth is now Postgres (M2.2):
/// the server populates this from the DB at startup (<c>ContentLoader</c>) and clients populate it
/// from the catalog synced over Mirror (<c>ContentCatalog</c>). The <see cref="Get"/> API is
/// unchanged — only where the data comes from moved (off <c>Resources/Items</c>).
/// </summary>
public class ItemRegistry : MonoBehaviour
{
    public static ItemRegistry Instance { get; private set; }

    readonly Dictionary<string, ItemDefinition> _items = new();

    void Awake() => Instance = this;

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>True once a content source (DB load or catalog sync) has populated the registry.</summary>
    public bool IsPopulated => _items.Count > 0;

    public void Register(ItemDefinition def)
    {
        if (def != null && !string.IsNullOrEmpty(def.itemId))
            _items[def.itemId] = def;
    }

    public ItemDefinition Get(string itemId)
        => string.IsNullOrEmpty(itemId) ? null : _items.GetValueOrDefault(itemId);

    /// <summary>
    /// Replace the registry contents from DB-backed snapshots — building a runtime
    /// <see cref="ItemDefinition"/> instance per row. Used by the server (from the DB) and by
    /// clients (from the synced catalog), so both sides build identical definitions.
    /// </summary>
    public void LoadFrom(IEnumerable<ItemSnapshot> snapshots)
    {
        _items.Clear();
        foreach (var s in snapshots)
            Register(Build(s));
    }

    static ItemDefinition Build(ItemSnapshot s)
    {
        var def = ScriptableObject.CreateInstance<ItemDefinition>();
        def.name             = string.IsNullOrEmpty(s.DisplayName) ? s.ItemId : s.DisplayName;
        def.itemId           = s.ItemId;
        def.displayName      = s.DisplayName;
        def.description      = s.Description;
        def.maxStackSize     = s.MaxStackSize;
        def.isEquippable     = s.IsEquippable;
        def.equipSlot        = (EquipSlot)s.EquipSlot;
        def.bonusStr         = s.BonusStr;
        def.bonusSta         = s.BonusSta;
        def.bonusAgi         = s.BonusAgi;
        def.bonusDex         = s.BonusDex;
        def.bonusInt         = s.BonusInt;
        def.bonusWis         = s.BonusWis;
        def.bonusCha         = s.BonusCha;
        def.weaponBaseDamage = s.WeaponBaseDamage;
        def.weaponDelay      = s.WeaponDelay;
        def.weaponRange      = s.WeaponRange;
        def.weaponCategory   = (WeaponCategory)s.WeaponCategory;
        def.buyPrice         = s.BuyPrice;
        def.sellPrice        = s.SellPrice;
        // icon_address (s.IconAddress) is resolved lazily via AssetResolver where the sprite is shown;
        // ItemDefinition has no icon field yet (inventory is text today). Stored on the row for 2.2's
        // asset-binding proof; UI wiring is a later pass.
        return def;
    }
}
