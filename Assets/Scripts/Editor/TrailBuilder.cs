using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 3.1.9 helper — "draw" an organic dirt trail by dropping waypoint markers and laying dirt ground tiles
/// between them. Workflow:
///   1. Create an empty GameObject (e.g. "TrailRoute") somewhere in the scene.
///   2. Add child empties positioned where you want the trail to pass / bend (in order, top→bottom).
///   3. Select the route object → Tools/Zones/Build Path Along Children.
/// It lays PolygonGeneric dirt tiles along each segment (aligned to the segment, tiles overlapping so there
/// are no gaps, slight texture variation at corners). The trail is purely cosmetic: colliders stripped +
/// excluded from the navmesh bake, so the walkable ground + pathing are untouched.
///
/// **Terrain-conforming:** each tile raycasts down to the actual ground surface and tilts to lie flush on the
/// slope, so the trail follows hilly terrain instead of burying/floating or cutting cliff-edge seams. Requires
/// the hills/ground to have colliders (Tools/Terrain/Add Mesh Colliders to Selection). Building geometry under
/// the TrellisHub is ignored so the trail snaps to terrain, not roofs.
///
/// Re-runnable: regenerates a single "PathTiles" child each run (your waypoint empties are preserved). Nudge a
/// marker and re-run. Only the XZ of each waypoint is used — the height comes from the raycast.
/// </summary>
public static class TrailBuilder
{
    const string GenEnv         = "Assets/Synty/PolygonGeneric/Prefabs/Environment/";
    const string TilesContainer = "PathTiles";
    const float  PathHeight     = 0.03f;  // just above the grass tiles (~0.02) on the flat ground
    const float  Overlap        = 0.9f;   // step = tile size × this (10% overlap → no seams)

    [MenuItem("Tools/Zones/Build Path Along Children")]
    public static void Build()
    {
        var route = Selection.activeGameObject;
        if (route == null)
        {
            Debug.LogWarning("[TrailBuilder] Select a route object (an empty with child waypoint empties) first.");
            return;
        }

        // Waypoints = the route's children in hierarchy order, minus the generated container.
        var waypoints = new List<Transform>();
        Transform existing = null;
        foreach (Transform c in route.transform)
        {
            if (c.name == TilesContainer) { existing = c; continue; }
            waypoints.Add(c);
        }
        if (waypoints.Count < 2)
        {
            Debug.LogWarning($"[TrailBuilder] '{route.name}' needs at least 2 child waypoint empties (found {waypoints.Count}).");
            return;
        }

        var dirt = LoadAll(GenEnv,
            "SM_Gen_Env_Ground_Dirt_01", "SM_Gen_Env_Ground_Dirt_02",
            "SM_Gen_Env_Ground_Dirt_03", "SM_Gen_Env_Ground_Dirt_04");
        if (dirt.Count == 0)
        {
            Debug.LogError($"[TrailBuilder] No dirt tile prefabs found under {GenEnv}.");
            return;
        }

        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var tiles = new GameObject(TilesContainer).transform;
        tiles.SetParent(route.transform, false);

        MeasureFootprint(dirt[0], out var size, out var centerOffset);
        float step = Mathf.Max(0.5f, Mathf.Min(size.x, size.z) * Overlap);

        Physics.SyncTransforms(); // make sure the hill/ground colliders are current for the raycasts
        var rng = new System.Random(20240705);
        int count = 0;
        for (int i = 0; i < waypoints.Count - 1; i++)
            count += LaySegment(tiles, dirt, waypoints[i].position, waypoints[i + 1].position, step, centerOffset, rng);

        ExcludeFromNavMesh(tiles.gameObject);

        Selection.activeGameObject = route;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[TrailBuilder] Laid {count} dirt tile(s) along {waypoints.Count} waypoint(s) on '{route.name}'. " +
                  "Cosmetic only (no colliders, nav-excluded). Nudge markers + re-run to reshape.");
    }

    static int LaySegment(Transform parent, List<GameObject> dirt, Vector3 aWorld, Vector3 bWorld,
                          float step, Vector3 centerOffset, System.Random rng)
    {
        // Work in XZ; the height + tilt come from the ground raycast per tile.
        Vector3 a = new(aWorld.x, 0f, aWorld.z);
        Vector3 b = new(bWorld.x, 0f, bWorld.z);
        Vector3 delta = b - a;
        float len = delta.magnitude;
        if (len < 0.01f) return 0;

        Vector3 dir = delta / len;
        int n = Mathf.CeilToInt(len / step);
        int placed = 0;

        for (int k = 0; k <= n; k++)
        {
            float t = Mathf.Min(k * step, len);
            Vector3 xz = a + dir * t;

            Vector3 surface, normal;
            if (!SampleGround(xz, out surface, out normal)) { surface = new Vector3(xz.x, PathHeight, xz.z); normal = Vector3.up; }

            // Lie flush on the slope: tile's up = surface normal, grid aligned to the path direction; 90° spins
            // vary the texture. Then place so the footprint centre sits on the surface point, lifted slightly
            // along the normal (no z-fighting).
            Vector3 fwd = Vector3.ProjectOnPlane(dir, normal).normalized;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(fwd, normal) * Quaternion.Euler(0f, 90f * rng.Next(4), 0f);

            var prefab = dirt[rng.Next(dirt.Count)];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.rotation = rot;
            go.transform.position = surface + normal * 0.04f - rot * centerOffset;
            StripColliders(go);
            placed++;
        }
        return placed;
    }

    static readonly RaycastHit[] _hits = new RaycastHit[16];

    // Nearest downward ground hit at this XZ, ignoring the village/trail geometry so the trail snaps to the
    // terrain (hills/ground) rather than a building roof or a previously-placed tile.
    static bool SampleGround(Vector3 xz, out Vector3 point, out Vector3 normal)
    {
        point = default; normal = Vector3.up;
        var origin = new Vector3(xz.x, 500f, xz.z);
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, 1000f, ~0, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue; bool found = false;
        for (int i = 0; i < n; i++)
        {
            var h = _hits[i];
            if (IsHubOrTrail(h.collider.transform)) continue;
            if (h.distance < best) { best = h.distance; point = h.point; normal = h.normal; found = true; }
        }
        return found;
    }

    static bool IsHubOrTrail(Transform t)
    {
        for (var x = t; x != null; x = x.parent)
            if (x.name == "TrellisHub" || x.name == TilesContainer) return true;
        return false;
    }

    // ── Helpers (self-contained, matching the TerrainSetup patterns) ────────────────────────────────
    static List<GameObject> LoadAll(string folder, params string[] names)
    {
        var list = new List<GameObject>();
        foreach (var n in names)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(folder + n + ".prefab");
            if (p != null) list.Add(p);
        }
        return list;
    }

    static void StripColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);
    }

    static void MeasureFootprint(GameObject prefab, out Vector3 size, out Vector3 center)
    {
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        probe.transform.position = Vector3.zero;
        probe.transform.rotation = Quaternion.identity;

        var renderers = probe.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            size = new Vector3(4, 0, 4);
            center = Vector3.zero;
            Object.DestroyImmediate(probe);
            return;
        }

        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        size = b.size;
        center = b.center; // probe at origin → pivot→center offset
        Object.DestroyImmediate(probe);
    }

    static void ExcludeFromNavMesh(GameObject go)
    {
        var modType = FindType("Unity.AI.Navigation.NavMeshModifier");
        if (modType == null) return;
        var mod = go.GetComponent(modType) ?? go.AddComponent(modType);
        var prop = modType.GetProperty("ignoreFromBuild");
        if (prop != null && prop.CanWrite) { prop.SetValue(mod, true); return; }
        var so = new SerializedObject(mod);
        var sp = so.FindProperty("m_IgnoreFromBuild");
        if (sp != null) { sp.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    static System.Type FindType(string fullName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }
}
