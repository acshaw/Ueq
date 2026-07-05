using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click authoring for the 3.1.5 class content (CS2/CS3/CS8). Creates the two missing signature
/// abilities (Wizard "Fire Bolt" nuke, Cleric "Minor Heal") with their effects as embedded sub-assets,
/// gives the existing Kick a real <see cref="DamageEffect"/> so the Warrior's ability actually hits, and
/// assigns each class's <c>startingAbilities</c> (which auto-populate the hotbar via
/// <see cref="PlayerAbilities.SetRaceClass"/>). Idempotent: re-running updates values + only adds an effect
/// if one of that kind isn't already present.
///
/// Base-stat balance is intentionally NOT touched here (it's a judgment call and would clobber Warrior's
/// Phase-2 tuning) — set per-class base stats in the Race & Class Editor (CS1). Menu:
/// <c>Tools/Character/Build Class Content</c>.
/// </summary>
public static class ClassContentSetup
{
    const string AbilitiesDir = "Assets/Resources/Abilities";
    const string ClassesDir   = "Assets/Resources/Classes";

    // 3.1.6 (CS6/CS7) — cosmetic class weapon props, attached to the right-hand bone by CharacterModelFactory.
    // Grip offsets are left at zero here and tuned by eye in the live create-form preview.
    const string WarriorSword = "Assets/Synty/PolygonAdventure/Prefabs/Weapons/SM_Wep_Sword_01.prefab";
    const string WizardStaff  = "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Prop_WizardStaff_01.prefab";
    const string ClericSceptre = "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Prop_Sceptre_01.prefab";

    [MenuItem("Tools/Character/Build Class Content")]
    public static void Build()
    {
        // Wizard nuke — single-target fire damage scaling on INT, costs mana, plays the shared Cast anim.
        var fireBolt = EnsureAbility("FireBolt", "fire_bolt", "Fire Bolt",
            "A bolt of fire that scorches a single enemy.",
            AbilityTargetType.SingleTarget, range: 20f, manaCost: 10, animTrigger: "Cast");
        EnsureEffect<DamageEffect>(fireBolt, fx => { fx.baseDamage = 12; fx.scalingStat = ScalingStatType.Int; fx.scalingFactor = 0.5f; });

        // Cleric heal — self-target for MVP (CS4), scaling on WIS, costs mana, plays the Cast anim.
        var minorHeal = EnsureAbility("MinorHeal", "minor_heal", "Minor Heal",
            "Channels a mending light, restoring your own health.",
            AbilityTargetType.Self, range: 0f, manaCost: 10, animTrigger: "Cast");
        EnsureEffect<HealEffect>(minorHeal, fx => { fx.baseHeal = 20; fx.scalingStat = ScalingStatType.Wis; fx.scalingFactor = 0.5f; });

        // Warrior Kick — was animation-only; give it a STR-scaling hit (warrior abilities are free → manaCost 0).
        var kick = AssetDatabase.LoadAssetAtPath<AbilityDefinition>($"{AbilitiesDir}/Kick.asset");
        if (kick != null)
            EnsureEffect<DamageEffect>(kick, fx => { fx.baseDamage = 8; fx.scalingStat = ScalingStatType.Str; fx.scalingFactor = 0.5f; });
        else
            Debug.LogWarning("[ClassContent] Kick.asset not found — Warrior Kick will remain damage-less.");

        // Assign starting abilities (auto-fills the hotbar). Wizard/Cleric had none; Warrior keeps Kick(+Taunt).
        AssignStarting("Wizard", fireBolt);
        AssignStarting("Cleric", minorHeal);
        EnsureContains("Warrior", kick);

        // 3.1.6 — class weapon props (grip offsets left at zero → tune in the preview). Assigned only if the
        // class has none yet, so re-running doesn't clobber grip offsets tuned in the Inspector.
        AssignWeaponProp("Warrior", WarriorSword);
        AssignWeaponProp("Wizard",  WizardStaff);
        AssignWeaponProp("Cleric",  ClericSceptre);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RaceClassRegistry.Invalidate();
        Debug.Log("[ClassContent] Built Fire Bolt + Minor Heal, added Kick damage, assigned starting abilities " +
                  "+ weapon props. Set per-class base stats in Tools/Race & Class Editor; tune grip offsets in " +
                  "the 3.1.6 preview. Check no roster body already ships a held weapon (WP2).");
    }

    static AbilityDefinition EnsureAbility(string fileName, string id, string displayName, string description,
        AbilityTargetType targeting, float range, int manaCost, string animTrigger)
    {
        string path = $"{AbilitiesDir}/{fileName}.asset";
        var ab = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
        if (ab == null)
        {
            ab = ScriptableObject.CreateInstance<AbilityDefinition>();
            AssetDatabase.CreateAsset(ab, path);
            Debug.Log($"[ClassContent] Created ability {path}.");
        }
        ab.abilityId     = id;
        ab.displayName   = displayName;
        ab.description   = description;
        ab.targetingType = targeting;
        ab.range         = range;
        ab.castTime      = 0f;
        ab.manaCost      = manaCost;
        ab.animTrigger   = animTrigger;
        EditorUtility.SetDirty(ab);
        return ab;
    }

    // Add an effect of type T as a sub-asset of the ability if it doesn't already have one, then configure it.
    static void EnsureEffect<T>(AbilityDefinition ability, System.Action<T> configure) where T : AbilityEffect
    {
        if (ability == null) return;
        var fx = ability.effects.Find(e => e is T) as T;
        if (fx == null)
        {
            fx = ScriptableObject.CreateInstance<T>();
            fx.name = $"{ability.name}_{typeof(T).Name}";
            AssetDatabase.AddObjectToAsset(fx, ability);
            ability.effects.Add(fx);
        }
        configure(fx);
        EditorUtility.SetDirty(fx);
        EditorUtility.SetDirty(ability);
    }

    static void AssignStarting(string className, AbilityDefinition ability)
    {
        var cls = AssetDatabase.LoadAssetAtPath<ClassDefinition>($"{ClassesDir}/{className}.asset");
        if (cls == null) { Debug.LogWarning($"[ClassContent] {className}.asset not found — skipped."); return; }
        if (ability == null) return;
        cls.startingAbilities = new List<AbilityDefinition> { ability };
        EditorUtility.SetDirty(cls);
    }

    static void EnsureContains(string className, AbilityDefinition ability)
    {
        if (ability == null) return;
        var cls = AssetDatabase.LoadAssetAtPath<ClassDefinition>($"{ClassesDir}/{className}.asset");
        if (cls == null) { Debug.LogWarning($"[ClassContent] {className}.asset not found — skipped."); return; }
        if (!cls.startingAbilities.Contains(ability)) { cls.startingAbilities.Add(ability); EditorUtility.SetDirty(cls); }
    }

    // Assign the class weapon prop only if unset — preserves grip offsets tuned by hand in the Inspector.
    static void AssignWeaponProp(string className, string prefabPath)
    {
        var cls = AssetDatabase.LoadAssetAtPath<ClassDefinition>($"{ClassesDir}/{className}.asset");
        if (cls == null) { Debug.LogWarning($"[ClassContent] {className}.asset not found — prop skipped."); return; }
        if (cls.weaponPropPrefab != null) return; // already assigned; don't clobber a hand-tuned setup

        var prop = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prop == null) { Debug.LogWarning($"[ClassContent] Weapon prop not found: {prefabPath} (pack not imported?)."); return; }
        cls.weaponPropPrefab = prop;
        EditorUtility.SetDirty(cls);
    }
}
