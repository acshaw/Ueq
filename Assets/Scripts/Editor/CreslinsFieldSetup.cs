using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 3.1.9 (reshaped) — shape Creslin's Field into a properly-sized, FLAT starter zone so the village + trails
/// drop onto flat ground and crossing the field hits the 3–5 min walk target (~280u at 1 u/s). Resizes the
/// walkable <c>Ground</c> to a flat field, re-tiles grass over it, rings a light perimeter (trees + east
/// rocks = boundary/backdrop per the lore), and moves the Thornwood <c>ZonePortal</c> + its return
/// <c>ZoneEntry</c> to the far north edge so the exit is a real walk from spawn.
///
/// Anchors to the scene's <c>NetworkStartPosition</c>. Non-destructive to your hand-placed content EXCEPT: it
/// resizes <c>Ground</c>, clears its own <c>CreslinsField</c> root + the redundant tool-made <c>SyntyTerrain</c>
/// grassland, and repositions the portal/entry. Your old scattered core hills can't be auto-identified —
/// delete them by hand afterward (the ground is now flat under them). Menu: <c>Tools/Zones/Reshape Creslins Field</c>.
/// </summary>
public static class CreslinsFieldSetup
{
    const string GenEnv = "Assets/Synty/PolygonGeneric/Prefabs/Environment/";
    const string AdvEnv = "Assets/Synty/PolygonAdventure/Prefabs/Environments/";

    // Walk 3 u/s / sprint 5 u/s. 1500u ≈ 8 min walk / 5 min run across. Safe vs the 5000u zone-offset spacing
    // (half-width 750 « 5000). Tune FieldLength/PortalOffsetZ to resize; grass tiles + perimeter auto-scale.
    const float FieldWidth    = 1500f; // E-W (X)
    const float FieldLength    = 1500f; // N-S (Z)  — spawn→portal ≈ 8 min walk / 5 min run
    const float SpawnFromSouth = 60f;   // spawn sits this far north of the south edge
    const float PortalOffsetZ  = 1380f; // Thornwood portal this far north of spawn (near the north edge)
    const float PerimeterInset = 10f;
    const float TreeSpacing    = 18f;   // perimeter tree interval (wide — the field edge is ~6000u now)
    const float NorthGapHalf   = 20f;   // clear gap at the north edge centre for the portal / "way north"
    const float MinTileStep    = 70f;   // cap the grass-tile count on a huge field by scaling tiles up to ≥ this

    [MenuItem("Tools/Zones/Reshape Creslins Field")]
    public static void Build()
    {
        Vector3 origin = FindSpawnOrigin();
        Vector3 center = new(origin.x, 0f, origin.z - SpawnFromSouth + FieldLength * 0.5f);

        ClearPrevious();
        EnsureFlatGround(center);

        var root = new GameObject("CreslinsField").transform;
        TileGrass(root, center);
        BuildPerimeter(root, center, origin);
        MovePortalNorth(origin);

        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[CreslinsField] Flattened + sized to {FieldWidth}x{FieldLength}u around spawn {origin}. " +
                  "NEXT: (1) delete your old hand-placed core hills (the ground is flat under them now) + any " +
                  "leftover 'SyntyTerrain'; (2) re-run Tools/Zones/Build Trellis Starter Hub (buildings sit flush " +
                  "on flat ground) + Build Path Along Children; (3) rebake the navmesh (Tools/Terrain/Rebake " +
                  "NavMesh); (4) SAVE. Then verify the walk north to the portal is ~3–5 min and the " +
                  "creslins⇄thornwood round-trip still works.");
    }

    static Vector3 FindSpawnOrigin()
    {
        var start = Object.FindFirstObjectByType<Mirror.NetworkStartPosition>();
        Vector3 p = start != null ? start.transform.position : new Vector3(0f, 0f, -5f);
        return new Vector3(p.x, 0f, p.z);
    }

    // Only clear the tool's own root — never auto-delete user content. The old 'SyntyTerrain' grassland + the
    // hand-placed hills are left for the user to remove by hand (logged), so nothing nested there is lost.
    static void ClearPrevious()
    {
        var go = GameObject.Find("CreslinsField");
        if (go != null) Object.DestroyImmediate(go);
    }

    // Resize (or create) the flat walkable Ground plane + its NavMeshSurface (bakes off the collider), renderer
    // hidden so the grass sits on top.
    static void EnsureFlatGround(Vector3 center)
    {
        var ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
        }
        ground.transform.position   = new Vector3(center.x, 0f, center.z);
        ground.transform.rotation   = Quaternion.identity;
        ground.transform.localScale = new Vector3(FieldWidth / 10f, 1f, FieldLength / 10f); // Unity plane = 10u @ scale 1

        var mr = ground.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;

        var surfaceType = FindType("Unity.AI.Navigation.NavMeshSurface");
        if (surfaceType != null)
        {
            var surface = ground.GetComponent(surfaceType) ?? ground.AddComponent(surfaceType);
            var so = new SerializedObject(surface);
            var geo = so.FindProperty("m_UseGeometry");
            if (geo != null)
            {
                int idx = System.Array.IndexOf(geo.enumNames, "PhysicsColliders");
                if (idx >= 0) geo.enumValueIndex = idx;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogWarning("[CreslinsField] AI Navigation package missing — add a NavMeshSurface to Ground + bake.");
    }

    static void TileGrass(Transform root, Vector3 center)
    {
        var variants = LoadAll(GenEnv,
            "SM_Gen_Env_Ground_Grass_Large_01", "SM_Gen_Env_Ground_Grass_Large_02", "SM_Gen_Env_Ground_Grass_Large_03");
        if (variants.Count == 0) { Debug.LogWarning("[CreslinsField] No grass tiles found — skipping ground tiling."); return; }

        MeasureFootprint(variants[0], out var size, out var centerOffset);
        // Scale tiles up so a big field doesn't spawn thousands of them. Synty ground tiles are flat-shaded
        // (no texture detail), so scaling doesn't degrade the look.
        float baseStep = Mathf.Max(1f, Mathf.Min(size.x, size.z));
        float tileScale = Mathf.Max(1f, MinTileStep / baseStep);
        float step = baseStep * tileScale;

        var tiles = new GameObject("GroundTiles").transform;
        tiles.SetParent(root, false);

        int nx = Mathf.CeilToInt(FieldWidth / step), nz = Mathf.CeilToInt(FieldLength / step);
        float startX = center.x - nx * step * 0.5f, startZ = center.z - nz * step * 0.5f;
        var rng = new System.Random(20240705);

        for (int i = 0; i < nx; i++)
        for (int j = 0; j < nz; j++)
        {
            var prefab = variants[rng.Next(variants.Count)];
            var tile = Instantiate(prefab, tiles);
            tile.transform.localScale *= tileScale;
            Vector3 cell = new(startX + (i + 0.5f) * step, 0.02f, startZ + (j + 0.5f) * step);
            tile.transform.position = new Vector3(cell.x - centerOffset.x * tileScale, 0.02f, cell.z - centerOffset.z * tileScale);
            tile.transform.Rotate(0f, 90f * rng.Next(4), 0f, Space.World);
            StripColliders(tile);
        }
    }

    // Light boundary ring just inside the edges (gap at the north centre for the portal). Trees keep colliders
    // (carve the edge); east edge gets rocks for the "hills rising toward the mountains" read.
    static void BuildPerimeter(Transform root, Vector3 center, Vector3 origin)
    {
        var trees = LoadAll(AdvEnv,
            "SM_Env_Tree_01", "SM_Env_Tree_02", "SM_Env_Tree_03", "SM_Env_Tree_04", "SM_Env_Tree_05",
            "SM_Env_TreePine_01", "SM_Env_TreePine_02", "SM_Env_TreePine_03");
        var rocks = LoadAll(AdvEnv, "SM_Env_Rock_01", "SM_Env_Rock_02", "SM_Env_Rock_03", "SM_Env_Rock_04", "SM_Env_Rock_05");
        if (trees.Count == 0) { Debug.LogWarning("[CreslinsField] No tree prefabs found — skipping perimeter."); return; }

        var frame = new GameObject("Perimeter").transform;
        frame.SetParent(root, false);
        var rng = new System.Random(770077);

        float halfW = FieldWidth * 0.5f - PerimeterInset;
        float halfL = FieldLength * 0.5f - PerimeterInset;
        float northZ = center.z + halfL;

        // Iterate the four edges; interleave a rock on the east edge for the rising-hills read.
        for (float x = -halfW; x <= halfW; x += TreeSpacing)
        {
            PlaceEdge(frame, trees, rocks, rng, new Vector3(center.x + x, 0f, center.z - halfL), false); // south
            // north edge: keep a gap around the portal (x ≈ origin.x) so the way north is open
            if (Mathf.Abs((center.x + x) - origin.x) > NorthGapHalf)
                PlaceEdge(frame, trees, rocks, rng, new Vector3(center.x + x, 0f, northZ), false);
        }
        for (float z = -halfL; z <= halfL; z += TreeSpacing)
        {
            PlaceEdge(frame, trees, rocks, rng, new Vector3(center.x - halfW, 0f, center.z + z), false); // west
            PlaceEdge(frame, trees, rocks, rng, new Vector3(center.x + halfW, 0f, center.z + z), true);   // east (rocks)
        }
    }

    static void PlaceEdge(Transform parent, List<GameObject> trees, List<GameObject> rocks, System.Random rng,
                          Vector3 pos, bool eastRocks)
    {
        var pool = eastRocks && rocks.Count > 0 && rng.Next(3) == 0 ? rocks : trees;
        var prefab = pool[rng.Next(pool.Count)];
        var go = Instantiate(prefab, parent);
        go.transform.position = pos + new Vector3((float)(rng.NextDouble() * 3 - 1.5), 0f, (float)(rng.NextDouble() * 3 - 1.5));
        go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
        go.transform.localScale *= 1f + ((float)rng.NextDouble() * 2f - 1f) * 0.25f;
        // keep colliders (boundary)
    }

    // Move the Thornwood portal (+ its from-Thornwood return entry) to the far north edge so the exit is a real
    // walk. The server ZoneManager reads transform positions at runtime, so moving them is sufficient.
    static void MovePortalNorth(Vector3 origin)
    {
        ZonePortal portal = null;
        foreach (var p in Object.FindObjectsByType<ZonePortal>(FindObjectsSortMode.None))
            if (p.targetZoneId == "thornwood") { portal = p; break; }

        if (portal == null)
        {
            Debug.LogWarning($"[CreslinsField] No ZonePortal→thornwood found — place one at ~{origin + new Vector3(0,0,PortalOffsetZ)} by hand.");
            return;
        }

        Vector3 oldPos = portal.transform.position;
        Vector3 newPos = new(origin.x, oldPos.y, origin.z + PortalOffsetZ);
        portal.transform.position = newPos;

        // Move the from-Thornwood arrival (nearest non-"default" entry to the old portal) just south of it.
        ZoneEntry ret = null; float best = float.MaxValue;
        foreach (var e in Object.FindObjectsByType<ZoneEntry>(FindObjectsSortMode.None))
        {
            if (e.entryId == "default") continue;
            float d = (e.transform.position - oldPos).sqrMagnitude;
            if (d < best) { best = d; ret = e; }
        }
        if (ret != null) ret.transform.position = newPos + new Vector3(0f, 0f, -5f);

        Debug.Log($"[CreslinsField] Moved Thornwood portal to {newPos}" +
                  (ret != null ? $" + return entry '{ret.entryId}' just south of it." : " (no non-default return entry found to move)."));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────
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

    static void MeasureFootprint(GameObject prefab, out Vector3 size, out Vector3 center)
    {
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        probe.transform.position = Vector3.zero;
        probe.transform.rotation = Quaternion.identity;
        var renderers = probe.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { size = new Vector3(4, 0, 4); center = Vector3.zero; Object.DestroyImmediate(probe); return; }
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        size = b.size; center = b.center;
        Object.DestroyImmediate(probe);
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
