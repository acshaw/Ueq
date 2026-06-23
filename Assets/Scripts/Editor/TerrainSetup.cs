using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds a Synty "PolygonAdventure / PolygonGeneric" grassland over the play area,
/// replacing the grey primitive ground + obstacle cubes with tiled grass, scattered
/// trees/rocks/bushes/flowers, and a small village cluster.
///
/// Design (kept robust because it can't be play-tested from here):
///  • The flat "Ground" plane is KEPT as the walkable collider + NavMeshSurface source,
///    with its MeshRenderer turned OFF so the grey no longer shows. Players walk on it
///    and the navmesh bakes off it exactly as before — visuals sit on top at y≈0.02.
///  • Tall props (trees, rocks, buildings) keep their colliders so they block movement
///    and carve the navmesh on bake.
///  • Small decorative foliage (bushes, flowers, grass tufts) has its colliders stripped
///    and lives under a root flagged with NavMeshModifier(ignoreFromBuild) so it never
///    speckles the navmesh.
///  • Tile size is measured from the actual prefab bounds, so tiling is seamless
///    regardless of pivot.
///
/// Re-runnable: clears the previous "SyntyTerrain" root and the grey "Obstacles" each run.
/// Deterministic: fixed seed, so the layout is stable across runs.
///
/// IMPORTANT: run this AFTER Tools/Setup All — Setup All recreates a grey Ground +
/// Obstacles, so always rebuild the terrain last. Then bake the navmesh
/// (Window > AI > Navigation) before pressing Play.
/// </summary>
public static class TerrainSetup
{
    const string GenEnv = "Assets/Synty/PolygonGeneric/Prefabs/Environment/";
    const string AdvEnv = "Assets/Synty/PolygonAdventure/Prefabs/Environments/";
    const string AdvBld = "Assets/Synty/PolygonAdventure/Prefabs/Buildings/";

    const float AreaHalf      = 55f;  // half-extent of the visual ground (110 x 110)
    const float SpawnClearR   = 12f;  // keep this radius around origin clear (player spawn)
    static readonly Vector3 VillageCenter = new(28f, 0f, 24f);
    const float VillageClearR = 15f;  // keep scatter out of the village footprint

    [MenuItem("Tools/Terrain/Build Synty Grassland")]
    public static void Build()
    {
        var rng = new System.Random(20240620);

        ClearPrevious();
        var ground = EnsureWalkableGround();

        var root = new GameObject("SyntyTerrain").transform;

        TileGround(root, rng);
        ScatterProps(root, rng);
        BuildVillage(root);

        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[TerrainSetup] Grassland built. Ground renderer hidden, navmesh source = " +
                  $"'{ground.name}'. Bake the navmesh (Window > AI > Navigation) before Play.");
    }

    /// <summary>
    /// Adds a MeshCollider to every mesh under the selected object(s) that doesn't
    /// already have a collider — so visual-only Synty hills/ground become solid and
    /// the CharacterController stops sinking through them. Select your terrain/hill
    /// root(s) in the Hierarchy, then run this.
    /// </summary>
    [MenuItem("Tools/Terrain/Add Mesh Colliders to Selection")]
    public static void AddMeshCollidersToSelection()
    {
        var roots = Selection.gameObjects;
        if (roots == null || roots.Length == 0)
        {
            Debug.LogWarning("[TerrainSetup] Nothing selected. Select your hill/ground objects in the Hierarchy first.");
            return;
        }

        int added = 0, skipped = 0;
        foreach (var root in roots)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<Collider>() != null) { skipped++; continue; }

                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh; // non-convex (static terrain) is correct for walking
                added++;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[TerrainSetup] Added {added} MeshCollider(s); {skipped} already had a collider. " +
                  "Bake the navmesh afterward if the new geometry should affect enemy pathing.");
    }

    [MenuItem("Tools/Terrain/Add Mesh Colliders to Selection", true)]
    static bool AddMeshCollidersToSelectionValidate() => Selection.gameObjects.Length > 0;

    /// <summary>
    /// Rebakes every NavMeshSurface in the scene from current geometry — run this after
    /// adding hill colliders so the navmesh follows the terrain and mobs can path over it.
    /// (Each surface bakes per its own agent/slope settings; raise Max Slope on the surface
    /// if steep hills aren't getting navmesh.)
    /// </summary>
    [MenuItem("Tools/Terrain/Rebake NavMesh")]
    public static void RebakeNavMesh()
    {
        var surfaceType = FindType("Unity.AI.Navigation.NavMeshSurface");
        if (surfaceType == null)
        {
            Debug.LogError("[TerrainSetup] AI Navigation package missing — can't rebake.");
            return;
        }

        var build = surfaceType.GetMethod("BuildNavMesh");
        var surfaces = Object.FindObjectsByType(surfaceType, FindObjectsSortMode.None);
        if (surfaces.Length == 0)
        {
            Debug.LogWarning("[TerrainSetup] No NavMeshSurface in the scene to bake.");
            return;
        }

        foreach (var s in surfaces) build?.Invoke(s, null);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[TerrainSetup] Rebaked {surfaces.Length} NavMeshSurface(s).");
    }

    [MenuItem("Tools/Terrain/Clear Synty Terrain")]
    public static void Clear()
    {
        ClearPrevious();
        var ground = GameObject.Find("Ground");
        var mr = ground != null ? ground.GetComponent<MeshRenderer>() : null;
        if (mr != null) mr.enabled = true; // restore the grey plane visual
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[TerrainSetup] Synty terrain cleared.");
    }

    // ── Setup steps ───────────────────────────────────────────────────────────

    static void ClearPrevious()
    {
        foreach (var n in new[] { "SyntyTerrain", "Obstacles" })
        {
            var go = GameObject.Find(n);
            if (go != null) Object.DestroyImmediate(go);
        }
    }

    /// Reuse the flat "Ground" plane as the walkable collider + navmesh source, hiding
    /// its grey visual. Create it if the scene doesn't have one.
    static GameObject EnsureWalkableGround()
    {
        var ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10, 1, 10); // 100 x 100 units
        }

        var mr = ground.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false; // hide grey; Synty ground sits on top

        // NavMeshSurface bakes off the flat Ground COLLIDER (still enabled), not render
        // meshes — so a hidden renderer / gapped visual tiles / collider-less props can't
        // leave holes. Tall props that ship with colliders still carve the navmesh.
        var surfaceType = FindType("Unity.AI.Navigation.NavMeshSurface");
        if (surfaceType != null)
        {
            var surface = ground.GetComponent(surfaceType) ?? ground.AddComponent(surfaceType);
            var so = new SerializedObject(surface);
            var geo = so.FindProperty("m_UseGeometry");
            if (geo != null)
            {
                int idx = System.Array.IndexOf(geo.enumNames, "PhysicsColliders");
                if (idx >= 0) geo.enumValueIndex = idx; // bake off colliders, not render meshes
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogWarning("[TerrainSetup] AI Navigation package missing — add a NavMeshSurface to Ground and bake.");

        return ground;
    }

    static void TileGround(Transform root, System.Random rng)
    {
        var variants = LoadAll(GenEnv,
            "SM_Gen_Env_Ground_Grass_Large_01",
            "SM_Gen_Env_Ground_Grass_Large_02",
            "SM_Gen_Env_Ground_Grass_Large_03");
        if (variants.Count == 0)
        {
            Debug.LogWarning("[TerrainSetup] No grass ground prefabs found — skipping ground tiling.");
            return;
        }

        // Measure the primary tile's footprint so tiling is seamless from any pivot.
        Vector3 size, centerOffset;
        MeasureFootprint(variants[0], out size, out centerOffset);
        float stepX = Mathf.Max(1f, size.x);
        float stepZ = Mathf.Max(1f, size.z);
        bool square = Mathf.Abs(size.x - size.z) < 0.1f;

        var tilesRoot = new GameObject("GroundTiles").transform;
        tilesRoot.SetParent(root, false);

        int nx = Mathf.CeilToInt((AreaHalf * 2f) / stepX);
        int nz = Mathf.CeilToInt((AreaHalf * 2f) / stepZ);
        float startX = -nx * stepX * 0.5f;
        float startZ = -nz * stepZ * 0.5f;

        for (int i = 0; i < nx; i++)
        for (int j = 0; j < nz; j++)
        {
            var prefab = variants[rng.Next(variants.Count)];
            var tile = Instantiate(prefab, tilesRoot);

            Vector3 cellCenter = new(startX + (i + 0.5f) * stepX, 0f, startZ + (j + 0.5f) * stepZ);
            tile.transform.position = cellCenter - new Vector3(centerOffset.x, 0f, centerOffset.z);
            tile.transform.position += new Vector3(0f, 0.02f, 0f); // just above the collider plane

            if (square)
                tile.transform.Rotate(0f, 90f * rng.Next(4), 0f, Space.World);

            StripColliders(tile);
        }
    }

    static void ScatterProps(Transform root, System.Random rng)
    {
        // Collider-bearing props (block movement, carve navmesh).
        var trees = LoadAll(AdvEnv,
            "SM_Env_Tree_01", "SM_Env_Tree_02", "SM_Env_Tree_03", "SM_Env_Tree_04", "SM_Env_Tree_05",
            "SM_Env_TreePine_01", "SM_Env_TreePine_02", "SM_Env_TreePine_03", "SM_Env_TreeBirch_01");
        var rocks = LoadAll(AdvEnv,
            "SM_Env_Rock_01", "SM_Env_Rock_02", "SM_Env_Rock_03", "SM_Env_Rock_04", "SM_Env_Rock_05");

        var solid = new GameObject("Props").transform;
        solid.SetParent(root, false);
        Scatter(solid, trees, 46, 18f, AreaHalf - 6f, rng, keepCollider: true,  scaleVar: 0.25f);
        Scatter(solid, rocks, 26, 14f, AreaHalf - 6f, rng, keepCollider: true,  scaleVar: 0.35f);

        // Decorative foliage (no colliders, excluded from navmesh bake).
        var bushes  = LoadAll(AdvEnv, "SM_Env_Bush_01", "SM_Env_Bush_02", "SM_Env_Bush_03", "SM_Env_Bush_04");
        var flowers = LoadAll(AdvEnv,
            "SM_Env_Flower_01", "SM_Env_Flower_02", "SM_Env_Flower_03", "SM_Env_Flower_04",
            "SM_Env_Flower_05", "SM_Env_Flower_06", "SM_Env_Flower_07", "SM_Env_Flower_08");
        var grass   = LoadAll(AdvEnv, "SM_Env_Grass_01", "SM_Env_Grass_02");

        var deco = new GameObject("Decoration").transform;
        deco.SetParent(root, false);
        Scatter(deco, bushes,  34, 8f, AreaHalf - 4f, rng, keepCollider: false, scaleVar: 0.3f);
        Scatter(deco, flowers, 70, 6f, AreaHalf - 4f, rng, keepCollider: false, scaleVar: 0.4f);
        Scatter(deco, grass,   60, 6f, AreaHalf - 4f, rng, keepCollider: false, scaleVar: 0.4f);

        ExcludeFromNavMesh(deco.gameObject); // keep flowers/grass off the navmesh
    }

    static void BuildVillage(Transform root)
    {
        var village = new GameObject("Village").transform;
        village.SetParent(root, false);

        // (prefab name, local offset from VillageCenter, Y rotation)
        var layout = new (string folder, string name, Vector3 off, float rot)[]
        {
            (AdvBld, "SM_Bld_Village_01", new Vector3( 0,   0,  0),   0),
            (AdvBld, "SM_Bld_Village_02", new Vector3( 8,   0,  2),  35),
            (AdvBld, "SM_Bld_Village_03", new Vector3(-7,   0,  3), -40),
            (AdvBld, "SM_Bld_Hut_01",     new Vector3( 2,   0, -8),  15),
            (AdvBld, "SM_Bld_Well_01",    new Vector3( 0,   0,  9),   0),
            (AdvBld, "SM_Bld_Stall_01",   new Vector3(-9,   0, -6),  90),
            (AdvBld, "SM_Bld_Stall_02",   new Vector3( 9,   0, -5), -90),
        };

        foreach (var (folder, name, off, rot) in layout)
        {
            var prefab = Load(folder, name);
            if (prefab == null) continue;
            var go = Instantiate(prefab, village);
            go.transform.position = VillageCenter + off;
            go.transform.rotation = Quaternion.Euler(0f, rot, 0f);
        }
    }

    // ── Scatter helper ──────────────────────────────────────────────────────────

    static void Scatter(Transform parent, List<GameObject> prefabs, int count,
                        float minR, float maxR, System.Random rng,
                        bool keepCollider, float scaleVar)
    {
        if (prefabs.Count == 0 || count <= 0) return;

        int placed = 0, guard = count * 12;
        while (placed < count && guard-- > 0)
        {
            float ang = (float)(rng.NextDouble() * System.Math.PI * 2.0);
            float r   = Mathf.Lerp(minR, maxR, (float)rng.NextDouble());
            float x = Mathf.Cos(ang) * r;
            float z = Mathf.Sin(ang) * r;

            if (x * x + z * z < SpawnClearR * SpawnClearR) continue;
            if ((new Vector3(x, 0, z) - VillageCenter).sqrMagnitude < VillageClearR * VillageClearR) continue;

            var prefab = prefabs[rng.Next(prefabs.Count)];
            var go = Instantiate(prefab, parent);
            go.transform.position = new Vector3(x, 0f, z);
            go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
            float s = 1f + ((float)rng.NextDouble() * 2f - 1f) * scaleVar;
            go.transform.localScale *= s;

            if (!keepCollider) StripColliders(go);
            placed++;
        }
    }

    // ── Asset / geometry helpers ────────────────────────────────────────────────

    static GameObject Load(string folder, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".prefab");
        if (prefab == null) Debug.LogWarning($"[TerrainSetup] Missing prefab: {folder}{name}.prefab");
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
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        return go;
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
        center = b.center; // probe sits at origin, so this is the pivot→center offset
        Object.DestroyImmediate(probe);
    }

    /// Adds NavMeshModifier(ignoreFromBuild) so this subtree is skipped during bake.
    static void ExcludeFromNavMesh(GameObject go)
    {
        var modType = FindType("Unity.AI.Navigation.NavMeshModifier");
        if (modType == null) return; // navmesh package absent — nothing to exclude against

        var mod = go.GetComponent(modType) ?? go.AddComponent(modType);
        var prop = modType.GetProperty("ignoreFromBuild");
        if (prop != null && prop.CanWrite) { prop.SetValue(mod, true); return; }

        // Fallback: set the serialized field directly.
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
