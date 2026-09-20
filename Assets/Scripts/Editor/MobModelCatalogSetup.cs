using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 3.1.10 Increment B — bootstraps + populates the <see cref="MobModelCatalog"/> so mob bodies are one-click to
/// register. Creates <c>Assets/Resources/MobModelCatalog.asset</c> if missing, then scans imported Synty
/// character prefabs (any pack's <c>Prefabs/Characters/</c> folder) plus the Simple Forest Animal pack, and
/// adds an entry per prefab it doesn't already reference. Idempotent — re-run after importing a new pack to
/// pick up only the new bodies; existing entries (including any modelId/controller you hand-edited) are never
/// touched.
///
/// Synty bodies are Humanoid, so they're added with no controller/speedParam (they retarget the shared
/// locomotion controller for free). Simple Forest Animal bodies are Generic rigs with their own per-species
/// controller (<see cref="ForestAnimalRigs"/> maps name prefix → shared controller, since several species
/// reuse one rig) and all of them use a "Speed_f" locomotion float, not the default "Speed".
///
/// After running: edit the asset in the Inspector — rename <c>modelId</c>s to whatever reads best (or to a mob
/// id for the convention path). Non-Humanoid bodies added by this tool already have their controller/speedParam
/// set; only Synty entries (or a new pack not covered here) need those set by hand.
/// </summary>
public static class MobModelCatalogSetup
{
    const string CatalogPath = "Assets/Resources/MobModelCatalog.asset";
    const string CharacterFolderMarker = "/Prefabs/Characters/";

    const string ForestAnimalFolder = "Assets/SimpleForestAnimal/Prefabs";
    const string ForestAnimalControllerDir = "Assets/SimpleForestAnimal/Models";
    const string ForestAnimalSpeedParam = "Speed_f";

    // Prefab name prefix → controller asset name (without extension). Wolf/Raccoon/Skunk share the Fox rig;
    // Doe/Stag/Moose share the Deer rig (confirmed by reading each .controller's parameters — all five use the
    // same "Speed_f"/"Eat_b" convention).
    static readonly (string prefix, string controllerName)[] ForestAnimalRigs =
    {
        ("Bear", "SFA_Animal_Bear"),
        ("Boar", "SFA_Animal_Boar"),
        ("Fox", "SFA_Animal_Fox"),
        ("Wolf", "SFA_Animal_Fox"),
        ("Raccoon", "SFA_Animal_Fox"),
        ("Skunk", "SFA_Animal_Fox"),
        ("Rabbit", "SFA_Animal_Rabbit"),
        ("Doe", "SFA_Animal_Deer"),
        ("Stag", "SFA_Animal_Deer"),
        ("Moose", "SFA_Animal_Deer"),
    };

    [MenuItem("Tools/Character/Build Mob Model Catalog")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MobModelCatalog>(CatalogPath);
        if (catalog == null)
        {
            Directory.CreateDirectory("Assets/Resources");
            catalog = ScriptableObject.CreateInstance<MobModelCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            Debug.Log($"[MobModelCatalog] Created {CatalogPath}");
        }

        // Track prefabs already referenced (by asset path) so re-runs don't duplicate.
        var known = new HashSet<string>();
        foreach (var e in catalog.entries)
            if (e.prefab != null) known.Add(AssetDatabase.GetAssetPath(e.prefab));

        int added = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Synty" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains(CharacterFolderMarker)) continue; // character bodies only
            if (known.Contains(path)) continue;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            catalog.entries.Add(new MobModelCatalog.Entry { modelId = prefab.name, prefab = prefab });
            known.Add(path);
            added++;
        }

        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { ForestAnimalFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (known.Contains(path)) continue;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var controller = ResolveForestAnimalController(prefab.name);
            if (controller == null)
                Debug.LogWarning($"[MobModelCatalog] '{prefab.name}': no rig mapping in ForestAnimalRigs — " +
                                  "added without a controller; set one by hand or it will show its bind pose.");

            catalog.entries.Add(new MobModelCatalog.Entry
            {
                modelId = prefab.name,
                prefab = prefab,
                animatorController = controller,
                speedParam = controller != null ? ForestAnimalSpeedParam : null,
            });
            known.Add(path);
            added++;
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        MobModelRegistry.Invalidate();

        Selection.activeObject = catalog;
        Debug.Log($"[MobModelCatalog] Added {added} new body model(s); {catalog.entries.Count} total. " +
                  "Edit the asset: rename modelIds as you like (or to a mob id for the convention path); set an " +
                  "animatorController only on non-Humanoid bodies not already covered by this tool. Referenced " +
                  "packs must be committed (not gitignored) so the prefab refs resolve.");
    }

    static RuntimeAnimatorController ResolveForestAnimalController(string prefabName)
    {
        foreach (var (prefix, controllerName) in ForestAnimalRigs)
        {
            if (!prefabName.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) continue;
            var path = $"{ForestAnimalControllerDir}/{controllerName}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
            if (controller == null)
                Debug.LogWarning($"[MobModelCatalog] Expected controller not found at '{path}'.");
            return controller;
        }
        return null;
    }
}
