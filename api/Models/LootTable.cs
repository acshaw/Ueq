namespace Ueq.ContentApi.Models;

/// <summary>EF entity for <c>loot_tables</c> (M2.7). Mapping-only; SQL runner owns the schema.</summary>
public class LootTable
{
    public string LootTableId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

    public List<LootItem> Items { get; set; } = new();
    public List<LootDropCount> DropCounts { get; set; } = new();
    public List<LootCoinTier> CoinTiers { get; set; } = new();
}

public class LootItem
{
    public long Id { get; set; }
    public string LootTableId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public int Weight { get; set; }
    public int SortOrder { get; set; }
}

public class LootDropCount
{
    public long Id { get; set; }
    public string LootTableId { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Weight { get; set; }
    public int SortOrder { get; set; }
}

public class LootCoinTier
{
    public long Id { get; set; }
    public string LootTableId { get; set; } = string.Empty;
    public int MinCopper { get; set; }
    public int MaxCopper { get; set; }
    public int Weight { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Editor-friendly shape: a loot table with its three weighted child lists.</summary>
public class LootTableDto
{
    public string LootTableId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<LootItemDto> Items { get; set; } = new();
    public List<LootDropCountDto> DropCounts { get; set; } = new();
    public List<LootCoinTierDto> CoinTiers { get; set; } = new();
}

public class LootItemDto { public string ItemId { get; set; } = string.Empty; public int Weight { get; set; } = 1; }
public class LootDropCountDto { public int Count { get; set; } public int Weight { get; set; } = 1; }
public class LootCoinTierDto { public int MinCopper { get; set; } public int MaxCopper { get; set; } public int Weight { get; set; } = 1; }

/// <summary>EF entity for <c>xp_levels</c> (M2.7) — one row of the shared XP curve.</summary>
public class XpLevel
{
    public int Level { get; set; }
    public int XpToNext { get; set; }
}
