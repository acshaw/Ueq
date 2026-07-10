using System.Collections.Generic;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 3.1.9 — a re-runnable curated "starter hub" for Creslin's Field: a small edge of Trellis village around the
/// spawn so a new player's first view reads as a place, not a flat scaffold. Places PolygonAdventure buildings
/// + framing trees + a north treeline (suggesting the Thornwood entrance) + light foliage, centered on a
/// SELECTED object (drop an empty where you want it — e.g. the NE corner) or the spawn point if nothing is
/// selected. Placements conform to the terrain surface. Curated baseline (SL3) — hand-tune in-editor afterward.
///
/// Non-destructive (SL4/SL7): does NOT touch the walkable ground, the spawn point, the Thornwood portal, or any
/// networking objects — it dresses around them and keeps a clear corridor from spawn to the portal. Buildings
/// get colliders (block + carve navmesh); foliage is collider-stripped + excluded from the bake.
///
/// Re-runnable: clears a prior "TrellisHub" root each run. Menu: <c>Tools/Zones/Build Trellis Starter Hub</c>.
/// After running: ensure the ground is dressed (grey placeholder → run Tools/Terrain/Build Synty Grassland),
/// then rebake + persist the navmesh (Tools/Terrain/Rebake NavMesh, or the Ground's NavMeshSurface → Bake).
/// </summary>
public static class TrellisHubSetup
{
    const string AdvBld = "Assets/Synty/PolygonAdventure/Prefabs/Buildings/";
    const string AdvEnv = "Assets/Synty/PolygonAdventure/Prefabs/Environments/";

    const string HubRoot = "TrellisHub";

    [MenuItem("Tools/Zones/Build Trellis Starter Hub")]
    public static void Build()
    {
        Vector3 origin = FindOrigin();   // capture BEFORE Clear (Clear may destroy a currently-selected old hub)
        Clear();
        Physics.SyncTransforms();        // terrain collider current for the ground-conform raycasts

        var root = new GameObject(HubRoot).transform;
        BuildVillage(root, origin);
        BuildTrees(root, origin);
        BuildFoliage(root, origin);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[TrellisHub] Built around {origin} (select an empty to reposition; nothing selected = spawn). " +
                  "Buildings/trees conform to the terrain surface. NEXT: rebake the navmesh (Tools/Terrain/Rebake " +
                  "NavMesh) and save. Hand-tune placement to taste.");
    }

    [MenuItem("Tools/Zones/Clear Trellis Starter Hub")]
    public static void Clear()
    {
        var go = GameObject.Find(HubRoot);
        if (go != null) Object.DestroyImmediate(go);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // Center the hub on the SELECTED object if one is selected (drop an empty where you want the village — e.g.
    // the NE corner — select it, then run). Otherwise anchor to the spawn point (falls back to ~0,·,-5).
    static Vector3 FindOrigin()
    {
        var sel = Selection.activeTransform;
        if (sel != null && !IsHub(sel))
            return new Vector3(sel.position.x, 0f, sel.position.z);
        var start = Object.FindFirstObjectByType<NetworkStartPosition>();
        Vector3 p = start != null ? start.transform.position : new Vector3(0f, 0f, -5f);
        return new Vector3(p.x, 0f, p.z);
    }

    static bool IsHub(Transform t)
    {
        for (var a = t; a != null; a = a.parent) if (a.name == HubRoot) return true;
        return false;
    }

    // Snap an XZ to the terrain surface, ignoring the hub's own geometry (so trees don't land on building roofs).
    static readonly RaycastHit[] _hits = new RaycastHit[16];
    static Vector3 OnGround(Vector3 world)
    {
        int n = Physics.RaycastNonAlloc(new Vector3(world.x, 1000f, world.z), Vector3.down, _hits, 2000f, ~0, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue; float y = world.y; bool found = false;
        for (int i = 0; i < n; i++)
        {
            if (IsHub(_hits[i].collider.transform)) continue;
            if (_hits[i].distance < bestDist) { bestDist = _hits[i].distance; y = _hits[i].point.y; found = true; }
        }
        return new Vector3(world.x, found ? y : world.y, world.z);
    }

    // ── Village (west of the spawn→portal corridor; corridor |x|<4, z∈[-2,12] kept clear) ──────────
    static void BuildVillage(Transform root, Vector3 o)
    {
        var village = new GameObject("Village").transform;
        village.SetParent(root, false);

        // (prefab, world offset from spawn origin [+X east, +Z north], Y rotation)
        var layout = new (string name, Vector3 off, float rot)[]
        {
            ("SM_Bld_Village_01", new Vector3(-14f, 0f,  4f),  90f),
            ("SM_Bld_Village_02", new Vector3(-16f, 0f, 13f),  60f),
            ("SM_Bld_Hut_01",     new Vector3(-11f, 0f, -3f), 110f),
            ("SM_Bld_Stall_01",   new Vector3( -7f, 0f,  6f),  90f),
            ("SM_Bld_Well_01",    new Vector3( -6f, 0f,  1f),   0f),
            ("SM_Bld_Village_03", new Vector3( 13f, 0f,  6f), -80f),
            ("SM_Bld_Stall_02",   new Vector3(  7f, 0f, -1f), -90f),
        };

        foreach (var (name, off, rot) in layout)
        {
            var prefab = Load(AdvBld, name);
            if (prefab == null) continue;
            var go = Instantiate(prefab, village);
            go.transform.position = OnGround(o + off);
            go.transform.rotation = Quaternion.Euler(0f, rot, 0f);
        }

        EnsureColliders(village.gameObject); // buildings block movement + carve the navmesh on bake
    }

    // ── Trees: side framing + a north treeline flanking the portal (Thornwood edge), corridor kept open ──
    static void BuildTrees(Transform root, Vector3 o)
    {
        var trees = LoadAll(AdvEnv,
            "SM_Env_Tree_01", "SM_Env_Tree_02", "SM_Env_Tree_03", "SM_Env_Tree_04", "SM_Env_Tree_05",
            "SM_Env_TreePine_01", "SM_Env_TreePine_02", "SM_Env_TreePine_03");
        if (trees.Count == 0) { Debug.LogWarning("[TrellisHub] No tree prefabs found — skipping framing."); return; }

        var pos = new[]
        {
            // side / rear framing
            new Vector3(-20f, 0f, -2f), new Vector3(-22f, 0f,  8f), new Vector3(-19f, 0f, 16f),
            new Vector3( 18f, 0f,  2f), new Vector3( 20f, 0f, 11f), new Vector3( 16f, 0f, -4f),
            new Vector3(-16f, 0f, -8f), new Vector3( 14f, 0f, -6f),
            // north treeline flanking the portal (x≈0 left open for the way north)
            new Vector3(-12f, 0f, 18f), new Vector3(-6f, 0f, 20f), new Vector3( 6f, 0f, 20f),
            new Vector3( 12f, 0f, 18f), new Vector3(-14f, 0f, 16f), new Vector3( 14f, 0f, 16f),
        };

        var group = new GameObject("Trees").transform;
        group.SetParent(root, false);
        var rng = new System.Random(20240705);
        foreach (var off in pos)
        {
            var prefab = trees[rng.Next(trees.Count)];
            var go = Instantiate(prefab, group);
            go.transform.position = OnGround(o + off);
            go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
            go.transform.localScale *= 1f + ((float)rng.NextDouble() * 2f - 1f) * 0.2f;
        }
        // Trees keep their colliders (block + carve navmesh) — no strip.
    }

    // ── Light foliage in the clearing (decorative; collider-stripped + nav-excluded), corridor + spots clear ──
    static void BuildFoliage(Transform root, Vector3 o)
    {
        var bushes  = LoadAll(AdvEnv, "SM_Env_Bush_01", "SM_Env_Bush_02", "SM_Env_Bush_03", "SM_Env_Bush_04");
        var flowers = LoadAll(AdvEnv,
            "SM_Env_Flower_01", "SM_Env_Flower_02", "SM_Env_Flower_03", "SM_Env_Flower_04",
            "SM_Env_Flower_05", "SM_Env_Flower_06");
        if (bushes.Count == 0 && flowers.Count == 0) return;

        var deco = new GameObject("Foliage").transform;
        deco.SetParent(root, false);
        var rng = new System.Random(424242);

        ScatterFoliage(deco, bushes,  18, o, rng, 0.3f);
        ScatterFoliage(deco, flowers, 34, o, rng, 0.4f);

        ExcludeFromNavMesh(deco.gameObject);
    }

    static void ScatterFoliage(Transform parent, List<GameObject> prefabs, int count, Vector3 o,
                               System.Random rng, float scaleVar)
    {
        if (prefabs.Count == 0 || count <= 0) return;
        int placed = 0, guard = count * 16;
        while (placed < count && guard-- > 0)
        {
            float x = (float)(rng.NextDouble() * 44.0 - 22.0);
            float z = (float)(rng.NextDouble() * 40.0 - 8.0);
            // Keep the spawn→portal corridor and the immediate spawn spot clear.
            if (Mathf.Abs(x) < 4.5f && z > -2f && z < 14f) continue;
            if (x * x + z * z < 3.5f * 3.5f) continue;

            var prefab = prefabs[rng.Next(prefabs.Count)];
            var go = Instantiate(prefab, parent);
            go.transform.position = OnGround(o + new Vector3(x, 0f, z)) + Vector3.up * 0.02f;
            go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
            go.transform.localScale *= 1f + ((float)rng.NextDouble() * 2f - 1f) * scaleVar;
            StripColliders(go);
            placed++;
        }
    }

    // ── Helpers (self-contained copies of the TerrainSetup patterns) ────────────────────────────────
    static GameObject Load(string folder, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".prefab");
        if (prefab == null) Debug.LogWarning($"[TrellisHub] Missing prefab: {folder}{name}.prefab");
        return prefab;
    }

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

    static GameObject Instantiate(GameObject prefab, Transform parent)
        => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);

    static void StripColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);
    }

    // Add a non-convex MeshCollider to any building mesh lacking a collider so it blocks + carves the navmesh
    // (Synty building prefabs don't all ship colliders).
    static void EnsureColliders(GameObject root)
    {
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
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
