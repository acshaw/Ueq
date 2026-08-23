using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 2.7.3 — the shared core of "take DB placement rows and make a scene match them," used identically by
/// <see cref="ZoneManager"/>'s runtime materialize-if-missing/refresh-if-present step (Stage A — the result
/// is ephemeral, never written to a scene asset) and the Editor's <c>Import Placements from Database</c>
/// tool (Stage B — the result IS meant to be saved). The only difference between the two call sites is what
/// happens to the object afterward (thrown away on server stop vs. marked dirty for the user to save) — this
/// class only ever creates/refreshes GameObjects, never persists or discards anything itself.
/// </summary>
public static class PlacementMaterializer
{
    /// <summary>Every <see cref="IWorldPlacement"/> already present in the scene, keyed by its PlacementId.</summary>
    public static Dictionary<string, GameObject> IndexScenePlacements(Scene scene)
    {
        var byId = new Dictionary<string, GameObject>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var placement in root.GetComponentsInChildren<IWorldPlacement>(true))
                byId[placement.PlacementId] = ((Component)placement).gameObject;
        return byId;
    }

    /// <summary>Splits rows into pass 1 (non-referencing marker types) and pass 2 (<c>SpawnPoint</c>, which
    /// may reference a pass-1 placement) — must be applied pass 1 first, in full, before pass 2.</summary>
    public static void SplitPasses(IEnumerable<WorldPlacementSnapshot> rows,
        out List<WorldPlacementSnapshot> pass1, out List<WorldPlacementSnapshot> pass2)
    {
        pass1 = new List<WorldPlacementSnapshot>();
        pass2 = new List<WorldPlacementSnapshot>();
        foreach (var row in rows)
            (row.MarkerType == "SpawnPoint" ? pass2 : pass1).Add(row);
    }

    /// <summary>For each row: refresh the matching scene object in place (config only — position/hierarchy
    /// stays whatever the scene already has, WP5), or materialize a brand-new one into <paramref name="scene"/>
    /// (position/rotation set from the row) if none exists yet. Updates <paramref name="byId"/> as it goes,
    /// so a second call for a later pass can resolve references against everything from this pass too.</summary>
    public static void ApplyRows(List<WorldPlacementSnapshot> rows, Dictionary<string, GameObject> byId, Scene scene)
    {
        foreach (var row in rows)
        {
            JObject data;
            try { data = JObject.Parse(row.Data); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Placement] {row.MarkerType} '{row.PlacementId}': malformed data JSON, skipped. {e.Message}");
                continue;
            }

            if (byId.TryGetValue(row.PlacementId, out var existing))
            {
                (existing.GetComponent(typeof(IWorldPlacement)) as IWorldPlacement)?.ApplyPlacementData(data);
                continue;
            }

            var factory = PlacementFactoryRegistry.Get(row.MarkerType);
            if (factory == null)
            {
                Debug.LogWarning($"[Placement] Zone '{scene.name}': no factory registered for marker type " +
                                 $"'{row.MarkerType}' (placement {row.PlacementId}) — skipped.");
                continue;
            }

            var go = new GameObject($"{row.MarkerType} (db)");
            go.transform.position = new Vector3(row.PosX ?? 0f, row.PosY ?? 0f, row.PosZ ?? 0f);
            go.transform.rotation = Quaternion.Euler(0f, row.RotY, 0f);
            var placement = factory.AddTo(go);
            placement.SetPlacementId(row.PlacementId);
            placement.ApplyPlacementData(data);
            if (scene.IsValid() && go.scene != scene)
                SceneManager.MoveGameObjectToScene(go, scene);
            byId[row.PlacementId] = go;
        }
    }

    /// <summary>Resolves cross-references for exactly the pass-2 rows just applied — a placement with no
    /// matching DB row was never touched by <see cref="ApplyRows"/>, so it's correctly skipped here too,
    /// leaving any hand-wired Inspector references on it completely alone.</summary>
    public static void ResolveReferences(List<WorldPlacementSnapshot> pass2Rows, Dictionary<string, GameObject> byId)
    {
        foreach (var row in pass2Rows)
        {
            if (!byId.TryGetValue(row.PlacementId, out var go)) continue;
            if (go.GetComponent(typeof(IReferencesOtherPlacements)) is IReferencesOtherPlacements refs)
                refs.ResolveReferences(byId);
        }
    }
}
