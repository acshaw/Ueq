using System.Collections.Generic;

/// <summary>Plain-data view of one ability (M2.9). Mirrors AbilityDefinition; tag refs carry both id and
/// display name so <c>AbilityRegistry</c> can build a runtime instance with no further lookups.</summary>
public struct AbilitySnapshot
{
    public string AbilityId;
    public string DisplayName;
    public string Description;
    public int    TargetingType;
    public float  Range;
    public float  CastTime;
    public int    ManaCost;
    public string AnimTrigger;

    public List<AbilityTagRefSnapshot>       Tags;          // semantic tags (AbilityDefinition.tags)
    public List<AbilityCooldownLinkSnapshot> CooldownLinks; // empty = uses GCD
    public List<AbilityEffectSnapshot>       Effects;       // applied in order
}

/// <summary>A resolved reference to an <c>ability_tags</c> row (id + display name).</summary>
public struct AbilityTagRefSnapshot
{
    public string TagId;
    public string DisplayName;
}

/// <summary>One row of <c>ability_cooldown_links</c> — a shared-timer key + duration.</summary>
public struct AbilityCooldownLinkSnapshot
{
    public string TagId;
    public string TagDisplayName;
    public float  Duration;
}

/// <summary>One row of <c>ability_effects</c> — effect_type + the shared amount/scaling shape (AB1).</summary>
public struct AbilityEffectSnapshot
{
    public string EffectType;    // "damage" | "heal"
    public int    BaseAmount;
    public int    ScalingStat;   // ScalingStatType enum
    public float  ScalingFactor;
}

/// <summary>Plain-data view of one <c>ability_tags</c> row, for the standalone Ability Tag content type.</summary>
public struct AbilityTagSnapshot
{
    public string TagId;
    public string DisplayName;
}
