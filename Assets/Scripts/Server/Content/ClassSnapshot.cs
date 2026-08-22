using System.Collections.Generic;

/// <summary>Plain-data view of one class (M2.10). Mirrors ClassDefinition, minus the weapon-prop
/// cosmetic fields (RC4 — those stay Unity-asset wiring on CharacterRoster, never enter the DB).</summary>
public struct ClassSnapshot
{
    public string ClassId;
    public string ClassName;
    public float  XpModifier;

    public int BaseStr, BaseSta, BaseAgi, BaseDex, BaseInt, BaseWis, BaseCha;

    public int   ClassBaseHP, HpPerLevel, StaCap;
    public float BaseStaRatio, StaGrowthRate;

    public int   ManaStatType; // ManaStatType enum
    public int   ClassBaseMana, ManaPerLevel, ManaCap;
    public float BaseManaRatio, ManaGrowthRate;

    // Offense/Defense are no longer per-class authored (2026-08-11 / 2026-08-13) — both are trained
    // per-character stats now (PlayerOffense.cs / PlayerAvoidanceSkills.cs).

    public List<string> StartingAbilityIds; // ordered ability ids (class_starting_abilities)
}
