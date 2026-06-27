namespace Ueq.ContentApi.Models;

/// <summary>
/// EF entity mapping onto the <c>items</c> table (M2.2). Mirrors the migration columns 1:1 (and the
/// Unity <c>ItemSnapshot</c>). Mapping-only — the SQL runner owns the schema (no EF Migrations);
/// this is hand-maintained to match. <c>ItemId</c> is the natural key (the cross-system item id).
/// </summary>
public class Item
{
    public string ItemId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MaxStackSize { get; set; } = 1;

    public bool IsEquippable { get; set; }
    public int EquipSlot { get; set; } = 11; // EquipSlot.Weapon

    public int BonusStr { get; set; }
    public int BonusSta { get; set; }
    public int BonusAgi { get; set; }
    public int BonusDex { get; set; }
    public int BonusInt { get; set; }
    public int BonusWis { get; set; }
    public int BonusCha { get; set; }

    public int WeaponBaseDamage { get; set; } = 10;
    public float WeaponDelay { get; set; } = 2f;
    public float WeaponRange { get; set; } = 3f;
    public int WeaponCategory { get; set; } // WeaponCategory.Might

    public int BuyPrice { get; set; }
    public int SellPrice { get; set; }

    public string? IconAddress { get; set; }

    public DateTime UpdatedAt { get; set; }
}
