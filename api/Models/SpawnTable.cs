namespace Ueq.ContentApi.Models;

/// <summary>EF entity for <c>spawn_tables</c> (M2.7.2). Inlined respawn timer. Mapping-only.</summary>
public class SpawnTable
{
    public string SpawnTableId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public float TimerBaseSeconds { get; set; } = 300f;
    public float TimerVariance { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<SpawnTableEntry> Entries { get; set; } = new();
}

/// <summary>EF entity for <c>spawn_table_entries</c> — one weighted mob entry.</summary>
public class SpawnTableEntry
{
    public long Id { get; set; }
    public string SpawnTableId { get; set; } = string.Empty;
    public string MobId { get; set; } = string.Empty;
    public int Weight { get; set; } = 1;
    public int GroupSize { get; set; } = 1;
    public int SortOrder { get; set; }
}

/// <summary>Editor-friendly shape: a spawn table, its timer, and its ordered entries.</summary>
public class SpawnTableDto
{
    public string SpawnTableId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public float TimerBaseSeconds { get; set; } = 300f;
    public float TimerVariance { get; set; }
    public List<SpawnEntryDto> Entries { get; set; } = new();
}

public class SpawnEntryDto
{
    public string MobId { get; set; } = string.Empty;
    public int Weight { get; set; } = 1;
    public int GroupSize { get; set; } = 1;
}
