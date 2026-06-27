/// <summary>
/// Plain-data view of one <c>items</c> row (M2.2). Mirrors <see cref="ItemDefinition"/>'s fields.
/// Used three ways: the server reads it from Postgres (<c>ItemRepository</c>), it is JSON-serialized
/// to sync the catalog to clients (<c>ContentCatalog</c>), and both sides build a runtime
/// <see cref="ItemDefinition"/> from it (<c>ItemRegistry.LoadFrom</c>). No Unity/Mirror types here so
/// it crosses the DB worker thread and JSON cleanly.
/// </summary>
public struct ItemSnapshot
{
    public string ItemId;
    public string DisplayName;
    public string Description;
    public int    MaxStackSize;

    public bool   IsEquippable;
    public int    EquipSlot;      // EquipSlot enum

    public int    BonusStr, BonusSta, BonusAgi, BonusDex, BonusInt, BonusWis, BonusCha;

    public int    WeaponBaseDamage;
    public float  WeaponDelay;
    public float  WeaponRange;
    public int    WeaponCategory;  // WeaponCategory enum

    public int    BuyPrice;
    public int    SellPrice;

    public string IconAddress;     // Addressables address (null/empty = none)
}
