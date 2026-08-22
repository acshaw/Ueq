namespace Ueq.ContentApi.Models;

/// <summary>EF entity for <c>classes</c> (M2.10). Mirrors ClassDefinition, minus the weapon-prop
/// cosmetic fields (those live on the Unity-side CharacterRoster asset, RC4 — never enter the DB).</summary>
public class Class
{
    public string ClassId { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public float XpModifier { get; set; } = 1f;

    public int BaseStr { get; set; } = 10;
    public int BaseSta { get; set; } = 10;
    public int BaseAgi { get; set; } = 10;
    public int BaseDex { get; set; } = 10;
    public int BaseInt { get; set; } = 10;
    public int BaseWis { get; set; } = 10;
    public int BaseCha { get; set; } = 10;

    public int ClassBaseHP { get; set; } = 15;
    public int HpPerLevel { get; set; } = 4;
    public int StaCap { get; set; } = 255;
    public float BaseStaRatio { get; set; } = 0.23f;
    public float StaGrowthRate { get; set; } = 0.15f;

    public int ManaStatType { get; set; } // 0 None, 1 Intellect, 2 Wisdom
    public int ClassBaseMana { get; set; }
    public int ManaPerLevel { get; set; }
    public int ManaCap { get; set; }
    public float BaseManaRatio { get; set; } = 0.23f;
    public float ManaGrowthRate { get; set; }

    // Offense/Defense are no longer authored per class (2026-08-11 / 2026-08-13) — both are trained
    // per-character stats now (PlayerOffense.cs / PlayerAvoidanceSkills.cs). classes.offense_base/
    // offense_per_level/defense_base/defense_per_level are still present in the DB but unmapped, same
    // as the tier_l1_*/tier_l20_* columns from before them, pending a follow-up cleanup migration.

    public DateTime UpdatedAt { get; set; }

    public List<ClassStartingAbility> StartingAbilities { get; set; } = new();
}

/// <summary>EF entity for <c>class_starting_abilities</c> — an ordered list of ability ids granted at creation.</summary>
public class ClassStartingAbility
{
    public long Id { get; set; }
    public string ClassId { get; set; } = string.Empty;
    public string AbilityId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>Editor-friendly shape: a class with its ordered starting-ability id list.</summary>
public class ClassDto
{
    public string ClassId { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public float XpModifier { get; set; } = 1f;

    public int BaseStr { get; set; } = 10;
    public int BaseSta { get; set; } = 10;
    public int BaseAgi { get; set; } = 10;
    public int BaseDex { get; set; } = 10;
    public int BaseInt { get; set; } = 10;
    public int BaseWis { get; set; } = 10;
    public int BaseCha { get; set; } = 10;

    public int ClassBaseHP { get; set; } = 15;
    public int HpPerLevel { get; set; } = 4;
    public int StaCap { get; set; } = 255;
    public float BaseStaRatio { get; set; } = 0.23f;
    public float StaGrowthRate { get; set; } = 0.15f;

    public int ManaStatType { get; set; }
    public int ClassBaseMana { get; set; }
    public int ManaPerLevel { get; set; }
    public int ManaCap { get; set; }
    public float BaseManaRatio { get; set; } = 0.23f;
    public float ManaGrowthRate { get; set; }

    public List<string> StartingAbilityIds { get; set; } = new();
}
