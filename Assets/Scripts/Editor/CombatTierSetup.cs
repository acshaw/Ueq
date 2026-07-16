using UnityEditor;
using UnityEngine;

/// <summary>
/// 5.1.1 (HR2) — one-click authoring for the class combat tier tables. Writes Warrior's Level 1 and
/// Level 20 tables verbatim from the design doc (§2.5, §2.11). Cleric and Wizard have no target table
/// in the design doc — user's call: ship a budget-ratio-scaled placeholder (proportional to each class's
/// starting-table stat-point budget, 75/90 and 55/90 of Warrior's) rather than guessing new numbers, and
/// swap in real hand-authored tables later with no code change (just re-author these two classes' assets
/// in Tools/Race &amp; Class Editor). Idempotent — re-running overwrites with the same values.
/// </summary>
public static class CombatTierSetup
{
    const string ClassesDir = "Assets/Resources/Classes";

    [MenuItem("Tools/Combat/Seed Class Combat Tables")]
    public static void Seed()
    {
        // Design doc §2.5 (Level 1) + §2.11 (Level 20) — verbatim.
        Apply("Warrior",
            new CombatTierTable { Miss = 17.5f, Glancing = 40f, Hit = 30f, SolidHit = 10f, GoodHit = 2.5f, Critical = 0f, Crippling = 0f },
            new CombatTierTable { Miss = 2f,    Glancing = 13f, Hit = 20f, SolidHit = 35f, GoodHit = 25f,  Critical = 3f, Crippling = 2f });

        // Placeholder: Warrior's Level1→Level20 delta scaled by budget ratio 75/90, added to Cleric's own
        // Level 1 table (§2.5). Internally consistent (sums to 100%) but NOT a real design target — flag
        // for replacement once a real Cleric curve exists.
        Apply("Cleric",
            new CombatTierTable { Miss = 20f, Glancing = 40f, Hit = 30f, SolidHit = 7.5f, GoodHit = 2.5f, Critical = 0f, Crippling = 0f },
            new CombatTierTable { Miss = 7.08f, Glancing = 17.5f, Hit = 21.67f, SolidHit = 28.33f, GoodHit = 21.25f, Critical = 2.5f, Crippling = 1.67f });

        // Placeholder: same method, budget ratio 55/90.
        Apply("Wizard",
            new CombatTierTable { Miss = 25f, Glancing = 40f, Hit = 25f, SolidHit = 7.5f, GoodHit = 2.5f, Critical = 0f, Crippling = 0f },
            new CombatTierTable { Miss = 15.53f, Glancing = 23.5f, Hit = 18.89f, SolidHit = 22.78f, GoodHit = 16.25f, Critical = 1.83f, Crippling = 1.22f });

        AssetDatabase.SaveAssets();
        Debug.Log("[CombatTier] Seeded Warrior (real §2.5/§2.11 data), Cleric + Wizard (budget-ratio-scaled " +
                  "placeholders — replace with real hand-authored tables when available).");
    }

    static void Apply(string className, CombatTierTable level1, CombatTierTable level20)
    {
        var cls = AssetDatabase.LoadAssetAtPath<ClassDefinition>($"{ClassesDir}/{className}.asset");
        if (cls == null) { Debug.LogWarning($"[CombatTier] {className}.asset not found — skipped."); return; }
        cls.combatTierTableLevel1  = level1;
        cls.combatTierTableLevel20 = level20;
        EditorUtility.SetDirty(cls);
    }
}
