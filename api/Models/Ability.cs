namespace Ueq.ContentApi.Models;

/// <summary>EF entity for <c>abilities</c> (M2.9). Mapping-only; SQL runner owns the schema.</summary>
public class Ability
{
    public string AbilityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TargetingType { get; set; } = 1; // 0=Self, 1=SingleTarget
    public float Range { get; set; } = 20f;
    public float CastTime { get; set; }
    public int ManaCost { get; set; }
    public string AnimTrigger { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

    public List<AbilityDefinitionTag> Tags { get; set; } = new();
    public List<AbilityCooldownLink> CooldownLinks { get; set; } = new();
    public List<AbilityEffectRow> Effects { get; set; } = new();
}

/// <summary>EF entity for <c>ability_tags</c> (M2.9) — a standalone reference list, its own content type.</summary>
public class AbilityTag
{
    public string TagId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>EF entity for <c>ability_definition_tags</c> — an ability's own semantic tags.</summary>
public class AbilityDefinitionTag
{
    public long Id { get; set; }
    public string AbilityId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>EF entity for <c>ability_cooldown_links</c> — a shared-timer key + duration.</summary>
public class AbilityCooldownLink
{
    public long Id { get; set; }
    public string AbilityId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public float Duration { get; set; } = 3f;
    public int SortOrder { get; set; }
}

/// <summary>EF entity for <c>ability_effects</c> — effect_type + the shared amount/scaling shape (AB1).</summary>
public class AbilityEffectRow
{
    public long Id { get; set; }
    public string AbilityId { get; set; } = string.Empty;
    public string EffectType { get; set; } = string.Empty; // "damage" | "heal"
    public int BaseAmount { get; set; }
    public int ScalingStat { get; set; } // ScalingStatType enum
    public float ScalingFactor { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Editor-friendly shape: an ability with its three ordered child lists.</summary>
public class AbilityDto
{
    public string AbilityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TargetingType { get; set; } = 1;
    public float Range { get; set; } = 20f;
    public float CastTime { get; set; }
    public int ManaCost { get; set; }
    public string AnimTrigger { get; set; } = string.Empty;

    public List<string> TagIds { get; set; } = new();
    public List<AbilityCooldownLinkDto> CooldownLinks { get; set; } = new();
    public List<AbilityEffectDto> Effects { get; set; } = new();
}

public class AbilityCooldownLinkDto { public string TagId { get; set; } = string.Empty; public float Duration { get; set; } = 3f; }
public class AbilityEffectDto { public string EffectType { get; set; } = string.Empty; public int BaseAmount { get; set; } public int ScalingStat { get; set; } public float ScalingFactor { get; set; } }
