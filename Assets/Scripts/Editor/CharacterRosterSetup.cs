using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click authoring for the 3.1.4 onboarding lineup (decision RM7). Creates the race + class assets the
/// roster references (Human/Warrior already exist; Dwarf, Wizard, Cleric are stamped with placeholder
/// stats — 3.1.5 tunes them), then builds <c>Resources/CharacterRoster.asset</c> with the seven legal
/// (gender, race, class) tuples and their Synty body prefabs resolved by path.
///
/// Idempotent: re-running updates the roster in place and only creates missing race/class assets. Model
/// prefabs live in the gitignored Synty packs — a missing one is warned about and left null (the create
/// form still works; PlayerModel falls back). Menu: <c>Tools/Character/Build Character Roster</c>.
/// </summary>
public static class CharacterRosterSetup
{
    const string RosterPath  = "Assets/Resources/CharacterRoster.asset";
    const string RacesDir    = "Assets/Resources/Races";
    const string ClassesDir  = "Assets/Resources/Classes";
    const string ControllerPath = "Assets/Animations/PlayerLocomotion.controller";

    // Body prefab paths (Synty packs). Dwarf shares one body across both its classes.
    const string MaleWarrior   = "Assets/Synty/PolygonAdventure/Prefabs/Characters/SM_Chr_Warrior_White.prefab";
    const string MaleWizard    = "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Chr_Male_Wizard_01.prefab";
    const string DwarfBody     = "Assets/Synty/PolygonFantasyRivals/Prefabs/Characters/SM_Chr_BR_Dwarf_01.prefab";
    const string FemaleWarrior = "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Chr_Female_Gypsy_01.prefab";
    const string FemaleWizard  = "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Chr_Female_Witch_01.prefab";
    const string FemaleCleric  = "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Chr_Female_Peasant_02.prefab";

    [MenuItem("Tools/Character/Build Character Roster")]
    public static void Build()
    {
        EnsureRace("Human");    // already present; created if somehow missing
        EnsureRace("Dwarf");
        EnsureClass("Warrior");
        EnsureClass("Wizard");
        EnsureClass("Cleric");

        var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterPath);
        if (roster == null)
        {
            roster = ScriptableObject.CreateInstance<CharacterRoster>();
            AssetDatabase.CreateAsset(roster, RosterPath);
        }

        roster.entries = new List<RosterEntry>
        {
            Entry(Gender.Male,   "Human", "Warrior", MaleWarrior),
            Entry(Gender.Male,   "Human", "Wizard",  MaleWizard),
            Entry(Gender.Male,   "Dwarf", "Warrior", DwarfBody),
            Entry(Gender.Male,   "Dwarf", "Cleric",  DwarfBody),
            Entry(Gender.Female, "Human", "Warrior", FemaleWarrior),
            Entry(Gender.Female, "Human", "Wizard",  FemaleWizard),
            Entry(Gender.Female, "Human", "Cleric",  FemaleCleric),
        };

        // 3.1.6 — the runtime create-form preview resolves the locomotion controller through the roster
        // (Resources-loadable) since it has no serialized scene ref of its own.
        roster.locomotionController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (roster.locomotionController == null)
            Debug.LogWarning($"[Roster] Locomotion controller not found at {ControllerPath} — the preview " +
                             "body will T-pose. Run Tools/Build Player Locomotion Controller first.");

        EditorUtility.SetDirty(roster);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CharacterRosterRegistry.Invalidate();
        RaceClassRegistry.Invalidate();
        Debug.Log($"[Roster] Built {RosterPath} with {roster.entries.Count} entries.");
    }

    static RosterEntry Entry(Gender gender, string race, string cls, string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            Debug.LogWarning($"[Roster] Model not found for {gender}/{race}/{cls}: {prefabPath} " +
                             "(pack not imported? entry created with no model).");
        return new RosterEntry { gender = gender, race = race, cls = cls, modelPrefab = prefab };
    }

    static void EnsureRace(string raceName)
    {
        string path = $"{RacesDir}/{raceName}.asset";
        if (AssetDatabase.LoadAssetAtPath<RaceDefinition>(path) != null) return;

        var r = ScriptableObject.CreateInstance<RaceDefinition>();
        r.raceName = raceName;
        // Placeholder racial flavor for the new Dwarf (tune in the Race & Class Editor).
        if (raceName == "Dwarf")
        {
            r.strMod = 2; r.staMod = 3; r.agiMod = -2; r.intMod = -2; r.wisMod = 1;
        }
        AssetDatabase.CreateAsset(r, path);
        Debug.Log($"[Roster] Created race asset {path}.");
    }

    static void EnsureClass(string className)
    {
        string path = $"{ClassesDir}/{className}.asset";
        if (AssetDatabase.LoadAssetAtPath<ClassDefinition>(path) != null) return;

        var c = ScriptableObject.CreateInstance<ClassDefinition>();
        c.className = className;
        // Placeholder archetype stats/formulas (per the CLAUDE.md HP/mana model) — 3.1.5 finalizes.
        switch (className)
        {
            case "Wizard": // caster: low HP, INT mana
                c.baseInt = 14;
                c.classBaseHP = 12; c.hpPerLevel = 1; c.staCap = 100; c.staGrowthRate = 0.12f;
                c.manaStatType = ManaStatType.Intellect;
                c.classBaseMana = 40; c.manaPerLevel = 5; c.manaCap = 200; c.manaGrowthRate = 0.18f;
                break;
            case "Cleric": // healer: mid HP, WIS mana
                c.baseWis = 14;
                c.classBaseHP = 13; c.hpPerLevel = 2; c.staCap = 140; c.staGrowthRate = 0.12f;
                c.manaStatType = ManaStatType.Wisdom;
                c.classBaseMana = 35; c.manaPerLevel = 4; c.manaCap = 200; c.manaGrowthRate = 0.16f;
                break;
            // Warrior (melee) uses the ClassDefinition defaults + ManaStatType.None.
        }
        AssetDatabase.CreateAsset(c, path);
        Debug.Log($"[Roster] Created class asset {path} (placeholder stats — tune in 3.1.5).");
    }
}
