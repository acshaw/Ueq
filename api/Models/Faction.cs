namespace Ueq.ContentApi.Models;

/// <summary>EF entity for <c>factions</c> (M2.6). Mapping-only; SQL runner owns the schema.</summary>
public class Faction
{
    public string FactionId { get; set; } = string.Empty;
    public string FactionName { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

    public List<FactionRelation> Relations { get; set; } = new();
    public List<RaceFactionDefault> RaceDefaults { get; set; } = new();
}

/// <summary>EF entity for <c>faction_relations</c> — one ally/hostile link (composite key).</summary>
public class FactionRelation
{
    public string FactionId { get; set; } = string.Empty;
    public string OtherFactionId { get; set; } = string.Empty;
    public string Relation { get; set; } = "hostile";   // "ally" | "hostile"
}

/// <summary>EF entity for <c>race_faction_defaults</c> — a race's starting score with a faction.</summary>
public class RaceFactionDefault
{
    public string Race { get; set; } = string.Empty;
    public string FactionId { get; set; } = string.Empty;
    public int Score { get; set; }
}

/// <summary>EF entity for <c>faction_thresholds</c> — one standing on the shared ladder.</summary>
public class FactionThreshold
{
    public string Name { get; set; } = string.Empty;
    public int MinScore { get; set; }
    public int SortOrder { get; set; }
    public string ConsiderText { get; set; } = string.Empty; // 5.4 (AG1)
}

/// <summary>Editor-friendly shape: a faction, its ally/hostile id lists, and its race defaults.</summary>
public class FactionDto
{
    public string FactionId { get; set; } = string.Empty;
    public string FactionName { get; set; } = string.Empty;
    public List<string> AllyIds { get; set; } = new();
    public List<string> HostileIds { get; set; } = new();
    public List<RaceDefaultDto> RaceDefaults { get; set; } = new();
}

public class RaceDefaultDto
{
    public string Race { get; set; } = string.Empty;
    public int Score { get; set; }
}
