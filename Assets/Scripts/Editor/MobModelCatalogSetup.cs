using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 3.1.10 Increment B — bootstraps + populates the <see cref="MobModelCatalog"/> so mob bodies are one-click to
/// register. Creates <c>Assets/Resources/MobModelCatalog.asset</c> if missing, then scans imported Synty
/// character prefabs (any pack's <c>Prefabs/Characters/</c> folder) and adds an entry per prefab it doesn't
/// already reference. Idempotent — re-run after importing a new pack to pick up only the new bodies; existing
/// entries (including any modelId/controller you hand-edited) are never touched.
///
/// After running: edit the asset in the Inspector — rename <c>modelId</c>s to whatever reads best (or to a mob
/// id for the convention path), and set an <c>animatorController</c> ONLY for non-Humanoid bodies.
/// </summary>
public static class MobModelCatalogSetup
{
    const string CatalogPath = "Assets/Resources/MobModelCatalog.asset";
    const string CharacterFolderMarker = "/Prefabs/Characters/";

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

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        MobModelRegistry.Invalidate();

        Selection.activeObject = catalog;
        Debug.Log($"[MobModelCatalog] Added {added} new body model(s); {catalog.entries.Count} total. " +
                  "Edit the asset: rename modelIds as you like (or to a mob id for the convention path); set an " +
                  "animatorController only on non-Humanoid bodies. Referenced packs must be committed (not " +
                  "gitignored) so the prefab refs resolve.");
    }
}
