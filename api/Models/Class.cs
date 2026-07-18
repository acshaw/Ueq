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

    public float TierL1Miss { get; set; } = 17.5f;
    public float TierL1Glancing { get; set; } = 40f;
    public float TierL1Hit { get; set; } = 30f;
    public float TierL1Solid { get; set; } = 10f;
    public float TierL1Good { get; set; } = 2.5f;
    public float TierL1Critical { get; set; }
    public float TierL1Crippling { get; set; }

    public float TierL20Miss { get; set; } = 2f;
    public float TierL20Glancing { get; set; } = 13f;
    public float TierL20Hit { get; set; } = 20f;
    public float TierL20Solid { get; set; } = 35f;
    public float TierL20Good { get; set; } = 25f;
    public float TierL20Critical { get; set; } = 3f;
    public float TierL20Crippling { get; set; } = 2f;

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

    public float TierL1Miss { get; set; } = 17.5f;
    public float TierL1Glancing { get; set; } = 40f;
    public float TierL1Hit { get; set; } = 30f;
    public float TierL1Solid { get; set; } = 10f;
    public float TierL1Good { get; set; } = 2.5f;
    public float TierL1Critical { get; set; }
    public float TierL1Crippling { get; set; }

    public float TierL20Miss { get; set; } = 2f;
    public float TierL20Glancing { get; set; } = 13f;
    public float TierL20Hit { get; set; } = 20f;
    public float TierL20Solid { get; set; } = 35f;
    public float TierL20Good { get; set; } = 25f;
    public float TierL20Critical { get; set; } = 3f;
    public float TierL20Crippling { get; set; } = 2f;

    public List<string> StartingAbilityIds { get; set; } = new();
}
