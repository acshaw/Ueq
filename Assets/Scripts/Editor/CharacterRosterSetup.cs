using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click authoring for the 3.1.4 onboarding lineup (decision RM7). Builds
/// <c>Resources/CharacterRoster.asset</c> with the seven legal (gender, race, class) tuples, their Synty
/// body prefabs, and (since M2.10, RC4) each class's cosmetic weapon-prop wiring — all pure Unity-asset
/// references, kept out of the DB now that races/classes themselves are DB-authored content.
///
/// Idempotent: re-running updates the roster in place. Model/prop prefabs live in the gitignored Synty
/// packs — a missing one is warned about and left null (the create form still works; PlayerModel falls
/// back). Race/class DATA (stats, formulas, starting abilities) is authored in the web Race &amp; Class
/// editors and seeded by <c>DatabaseSeeder</c> — this tool only wires art. Menu:
/// <c>Tools/Character/Build Character Roster</c>.
/// </summary>
public static class CharacterRosterSetup
{
    const string RosterPath     = "Assets/Resources/CharacterRoster.asset";
    const string ControllerPath = "Assets/Animations/PlayerLocomotion.controller";

    // Body prefab paths (Synty packs). Dwarf shares one body across both its classes.
    const string MaleWarrior   = "Assets/Synty/PolygonAdventure/Prefabs/Characters/SM_Chr_Warrior_White.prefab";
    const string MaleWizard    = "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Chr_Male_Wizard_01.prefab";
    const string DwarfBody     = "Assets/Synty/PolygonFantasyRivals/Prefabs/Characters/SM_Chr_BR_Dwarf_01.prefab";
    const string FemaleWarrior = "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Chr_Female_Gypsy_01.prefab";
    const string FemaleWizard  = "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Chr_Female_Witch_01.prefab";
    const string FemaleCleric  = "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Chr_Female_Peasant_02.prefab";

    // Weapon prop paths (3.1.6; moved here from the retired ClassContentSetup.cs by M2.10 RC4).
    const string WarriorSword  = "Assets/Synty/PolygonAdventure/Prefabs/Weapons/SM_Wep_Sword_01.prefab";
    const string WizardStaff   = "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Prop_WizardStaff_01.prefab";
    const string ClericSceptre = "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Prop_Sceptre_01.prefab";

    [MenuItem("Tools/Character/Build Character Roster")]
    public static void Build()
    {
        var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterPath);
        if (roster == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
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

        // M2.10 (RC4) — preserve any grip offsets already hand-tuned on the existing roster; only fill in
        // the prop prefab + a default zero offset for entries that don't exist yet.
        roster.classWeaponProps = MergeWeaponProps(roster.classWeaponProps,
            WeaponProp("Warrior", WarriorSword),
            WeaponProp("Wizard",  WizardStaff),
            WeaponProp("Cleric",  ClericSceptre));

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
        Debug.Log($"[Roster] Built {RosterPath} with {roster.entries.Count} entries + " +
                  $"{roster.classWeaponProps.Count} weapon prop(s).");
    }

    static RosterEntry Entry(Gender gender, string race, string cls, string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            Debug.LogWarning($"[Roster] Model not found for {gender}/{race}/{cls}: {prefabPath} " +
                             "(pack not imported? entry created with no model).");
        return new RosterEntry { gender = gender, race = race, cls = cls, modelPrefab = prefab };
    }

    static ClassWeaponProp WeaponProp(string className, string prefabPath)
    {
        var prop = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prop == null)
            Debug.LogWarning($"[Roster] Weapon prop not found for {className}: {prefabPath} (pack not imported?).");
        return new ClassWeaponProp { className = className, prop = prop };
    }

    // Preserve hand-tuned grip offsets from the existing list; only the prop prefab is (re)assigned.
    static List<ClassWeaponProp> MergeWeaponProps(List<ClassWeaponProp> existing, params ClassWeaponProp[] fresh)
    {
        var byClass = new Dictionary<string, ClassWeaponProp>();
        if (existing != null)
            foreach (var w in existing) byClass[w.className] = w;

        var result = new List<ClassWeaponProp>();
        foreach (var w in fresh)
        {
            var merged = w;
            if (byClass.TryGetValue(w.className, out var old))
            {
                merged.gripPositionOffset = old.gripPositionOffset;
                merged.gripEulerOffset    = old.gripEulerOffset;
            }
            result.Add(merged);
        }
        return result;
    }
}
