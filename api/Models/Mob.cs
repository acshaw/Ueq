namespace Ueq.ContentApi.Models;

/// <summary>
/// EF entity mapping onto the <c>mobs</c> table (M2.5). Mirrors MobDefinition. References other content
/// by string id (faction/conversation/loot/vendor) + a registered spawnable prefab name. Mapping-only.
/// </summary>
public class Mob
{
    public string MobId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int MobLevel { get; set; } = 1;
    public string? PrefabAddress { get; set; }

    public int MaxHealth { get; set; } = 10;
    public int AttackDamage { get; set; } = 1;
    public float AttackInterval { get; set; } = 2f;
    public float AttackRange { get; set; } = 2f;

    public int MovementType { get; set; } = 1; // 0 Stationary, 1 Wander
    public float MoveSpeed { get; set; } = 3.5f;
    public float WanderRadius { get; set; } = 10f;
    public float WanderPauseMin { get; set; } = 2f;
    public float WanderPauseMax { get; set; } = 6f;

    public float PerceptionRadius { get; set; } = 20f;
    public int BaseAggroThreat { get; set; } = 1;

    public string? FactionId { get; set; }
    public string AggroMaxStanding { get; set; } = "Threatening";
    public string WarningMaxStanding { get; set; } = "Apprehensive";

    public string? ConversationSetId { get; set; }
    public string? LootTableId { get; set; }
    public int XpReward { get; set; }

    public string? VendorId { get; set; }
    public string VendorOpenKeyword { get; set; } = "wares";

    public DateTime UpdatedAt { get; set; }

    public List<MobFactionHit> FactionHits { get; set; } = new();   // M2.7.1

    // 5.1.1 (HR5) / 5.1.2 (AV3) / 2.12 (SK5) — combat pipeline data, authored per mob.
    public int WeaponCategory { get; set; } // WeaponCategory: 0 Might, 1 Finesse
    public int WeaponSkill { get; set; }

    public float TierMiss { get; set; } = 17.5f;
    public float TierGlancing { get; set; } = 40f;
    public float TierHit { get; set; } = 30f;
    public float TierSolid { get; set; } = 10f;
    public float TierGood { get; set; } = 2.5f;
    public float TierCritical { get; set; }
    public float TierCrippling { get; set; }

    public bool AttackIsParryable { get; set; } = true;

    public float AvoidanceAgility { get; set; } = 20f;
    public float AvoidanceDexterity { get; set; } = 20f;
}

/// <summary>EF entity for <c>mob_faction_hits</c> (M2.7.1) — a standing change applied to the killer.</summary>
public class MobFactionHit
{
    public long Id { get; set; }
    public string MobId { get; set; } = string.Empty;
    public string FactionId { get; set; } = string.Empty;
    public int Delta { get; set; }
    public int SortOrder { get; set; }
}
