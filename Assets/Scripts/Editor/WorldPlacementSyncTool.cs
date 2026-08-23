using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 2.7.3 (Stage A) — exports every <see cref="IWorldPlacement"/> found in the currently open, zone-mapped
/// scene(s) into the <c>world_placements</c> table: always upserts what it finds (WP4), then offers to
/// delete any DB row for those zones that has no matching scene object, gated on an explicit confirm
/// dialog — never automatic. Writes directly to Postgres via <see cref="Database.OpenEditorConnection"/>,
/// matching every other <c>Tools/Database</c>/<c>Tools/Zones</c> utility in this project — no dependency on
/// the ASP.NET API being run locally (that API is only for the Stage C web editor).
/// </summary>
static class WorldPlacementSyncTool
{
    [MenuItem("Tools/Zones/Sync Placements to Database")]
    static void Sync()
    {
        var catalog = Resources.Load<ZoneCatalog>(ZoneCatalog.ResourcePath);
        if (catalog == null)
        {
            Debug.LogError("[Placement] No ZoneCatalog found — run Tools/Zones/Build Zone Scenes first.");
            return;
        }

        // Group every open, zone-mapped scene's placements by resolved zoneId (WP2 — the scene IS the zone,
        // never a hand-authored field). Assign a PlacementId to anything missing one along the way.
        var byZone = new Dictionary<string, List<IWorldPlacement>>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            var zoneDef = catalog.zones.FirstOrDefault(z => z != null && z.sceneName == scene.name);
            if (zoneDef == null)
            {
                Debug.LogWarning($"[Placement] Open scene '{scene.name}' doesn't match any ZoneCatalog entry — skipped.");
                continue;
            }

            bool sceneDirtied = false;
            var placements = new List<IWorldPlacement>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var p in root.GetComponentsInChildren<IWorldPlacement>(true))
            {
                if (string.IsNullOrEmpty(p.PlacementId))
                {
                    p.SetPlacementId(System.Guid.NewGuid().ToString());
                    sceneDirtied = true;
                }
                placements.Add(p);
            }

            if (placements.Count > 0)
                byZone[zoneDef.zoneId] = placements;
            if (sceneDirtied)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        if (byZone.Count == 0)
        {
            Debug.Log("[Placement] No IWorldPlacement objects found in any open, zone-mapped scene.");
            return;
        }

        using var conn = Database.OpenEditorConnection();
        var repo = new WorldPlacementRepository();

        int created = 0, updated = 0;
        var deletionCandidates = new List<WorldPlacementSnapshot>();

        foreach (var (zoneId, placements) in byZone)
        {
            var existingRows = repo.LoadForZone(conn, zoneId).ToDictionary(r => r.PlacementId);

            foreach (var p in placements)
            {
                var t = ((Component)p).transform;
                bool isNew = !existingRows.ContainsKey(p.PlacementId);
                var row = new WorldPlacementSnapshot
                {
                    PlacementId = p.PlacementId,
                    ZoneId      = zoneId,
                    MarkerType  = p.MarkerType,
                    PosX        = t.position.x,
                    PosY        = t.position.y,
                    PosZ        = t.position.z,
                    RotY        = t.eulerAngles.y,
                    Data        = p.CapturePlacementData().ToString(Formatting.None),
                };
                repo.Upsert(conn, row);
                if (isNew) created++; else updated++;
            }

            var scenePlacementIds = new HashSet<string>(placements.Select(p => p.PlacementId));
            foreach (var kv in existingRows)
                if (!scenePlacementIds.Contains(kv.Key))
                    deletionCandidates.Add(kv.Value);
        }

        int deleted = 0;
        if (deletionCandidates.Count > 0)
            deleted = ConfirmAndDelete(conn, repo, deletionCandidates);

        int skipped = deletionCandidates.Count - deleted;
        Debug.Log($"[Placement] Sync complete — {created} created, {updated} updated, {deleted} deleted" +
                  (skipped > 0 ? $", {skipped} skipped (declined)." : "."));
    }

    // WP4 — never automatic. Anything in the DB for a synced zone but not found among that zone's current
    // scene placements is only ever a *candidate*; this is the sole place sync deletes a DB row at all.
    static int ConfirmAndDelete(Npgsql.NpgsqlConnection conn, WorldPlacementRepository repo,
        List<WorldPlacementSnapshot> candidates)
    {
        var msg = new StringBuilder();
        msg.AppendLine($"{candidates.Count} DB placement(s) have no matching object in the currently open scene(s):");
        foreach (var row in candidates.Take(20))
            msg.AppendLine($"  • [{row.ZoneId}] {row.MarkerType} {row.PlacementId}");
        if (candidates.Count > 20)
            msg.AppendLine($"  ... and {candidates.Count - 20} more");
        msg.AppendLine();
        msg.AppendLine("Delete them from the database? If only part of a zone's scene is open right now, " +
                        "choose Keep — nothing is lost, and they'll be reconsidered next sync.");

        if (!EditorUtility.DisplayDialog("Sync Placements — deletion candidates", msg.ToString(), "Delete", "Keep"))
            return 0;

        foreach (var row in candidates)
            repo.Delete(conn, row.PlacementId);
        return candidates.Count;
    }
}
