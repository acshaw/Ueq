using System.Collections.Generic;
using UnityEngine;

public enum ManaStatType { None, Intellect, Wisdom }

/// <summary>
/// Runtime-only since M2.10 — built by <see cref="RaceClassRegistry"/> from a DB-backed
/// <see cref="ClassSnapshot"/> (server load or client catalog sync). No longer authored as an asset;
/// author classes in the web Class Editor. The weapon-prop cosmetic fields (3.1.6) live on
/// <see cref="CharacterRoster"/> instead (RC4) — pure Unity-asset wiring, not DB content.
/// </summary>
public class ClassDefinition : ScriptableObject
{
    public string className  = "Warrior";
    public float  xpModifier = 1f;

    [Header("Base Stats")]
    public int baseStr = 10;
    public int baseSta = 10;
    public int baseAgi = 10;
    public int baseDex = 10;
    public int baseInt = 10;
    public int baseWis = 10;
    public int baseCha = 10;

    [Header("HP Formula")]
    public int   classBaseHP   = 15;
    public int   hpPerLevel    = 4;
    public int   staCap        = 255;
    public float baseStaRatio  = 0.23f;
    public float staGrowthRate = 0.15f;

    [Header("Mana Formula")]
    public ManaStatType manaStatType  = ManaStatType.None;
    public int   classBaseMana  = 0;
    public int   manaPerLevel   = 0;
    public int   manaCap        = 0;
    public float baseManaRatio  = 0.23f;
    public float manaGrowthRate = 0f;

    // Offense (5.1.5) is no longer authored per class (2026-08-11) — ATK = EffectiveSkill (trained
    // weapon skill + relevant stat × 0.1, §2.10) + trained Offense (a persisted per-character stat,
    // PlayerOffense.cs, 2026-08-13 follow-up). A class's ATK differentiates purely through its base
    // stats now, not a separate authored knob.
    //
    // Defense (5.1.5 follow-up) is likewise no longer authored per class as of 2026-08-13 — it's a
    // trained per-character stat mirroring Offense exactly (PlayerAvoidanceSkills.cs), not a class
    // formula. classes.defense_base/defense_per_level remain in the DB, unmapped, same treatment as
    // offense_base/offense_per_level before them.

    [Header("Abilities")]
    [Tooltip("Ability ids granted at character creation (PlayerAbilities.SetRaceClass populates the " +
             "hotbar from these). Ability ids, not asset refs, since M2.9 moved abilities to the DB.")]
    public List<string> startingAbilities = new();
}
