using System.Linq;
using Npgsql;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2026-09-19 — syncs the known model ids in <see cref="MobModelCatalog"/> into the <c>mob_models</c>
/// lookup table, so the web Mob Editor can offer a dropdown instead of requiring the exact catalog
/// modelId string to be typed by hand (the gap flagged as "Increment C — optional next" in the 3.1.10
/// devplan). Full replace (delete-then-insert) — this table is a pure derived cache of the catalog with
/// no user-authored content of its own, so there's nothing to lose by re-deriving it each run, unlike
/// Tools/Zones/Sync Placements to Database's confirm-gated deletion of scene-authored content. Writes
/// directly to Postgres via <see cref="Database.OpenEditorConnection"/>, matching every other
/// Tools/Database and Tools/Zones utility in this project.
/// </summary>
static class MobModelSyncTool
{
    const string CatalogPath = "Assets/Resources/MobModelCatalog.asset";

    [MenuItem("Tools/Character/Sync Mob Model Catalog to Database")]
    static void Sync()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MobModelCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[MobModel] No catalog at {CatalogPath} — run " +
                            "Tools/Character/Build Mob Model Catalog first.");
            return;
        }

        var ids = catalog.entries
            .Select(e => e.modelId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        using var conn = Database.OpenEditorConnection();

        using (var del = new NpgsqlCommand("DELETE FROM mob_models", conn))
            del.ExecuteNonQuery();

        foreach (var id in ids)
        {
            using var ins = new NpgsqlCommand("INSERT INTO mob_models (model_id) VALUES (@id)", conn);
            ins.Parameters.AddWithValue("id", id);
            ins.ExecuteNonQuery();
        }

        Debug.Log($"[MobModel] Synced {ids.Count} model id(s) from the catalog to the database. " +
                  "Re-run any time the catalog changes — the web Mob Editor's Body Model dropdown reads " +
                  "this table.");
    }
}
