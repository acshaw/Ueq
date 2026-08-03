using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 3.1.9 — registers the Synty tree prefabs as TreePrototypes on the ZoneTerrain so you can use Unity's built-in
/// <b>Paint Trees</b> brush to draw trees directly onto the terrain (GPU-instanced — cheap for thousands).
/// Menu: <c>Tools/Zones/Add Synty Trees to Terrain</c>.
///
/// After running: select the terrain → Terrain component → <b>Paint Trees</b> tab → pick a tree → brush to draw
/// (tune Brush Size / Tree Density / Width+Height). <c>Terrain → Mass Place Trees</c> fills the whole zone at once.
///
/// Colliders/navmesh caveat: terrain-painted trees create their colliders at RUNTIME, so a bake-time
/// NavMeshSurface may not carve them — mobs could path through painted trees. Use painting for visual density;
/// for boundary trees that MUST block movement + navmesh, keep using <c>Tools/Zones/Scatter Props</c> (those are
/// real GameObjects with colliders). Enable "Enable Tree Colliders" in Terrain Settings for player collision.
/// </summary>
public static class TerrainTreePalette
{
    const string AdvEnv = "Assets/Synty/PolygonAdventure/Prefabs/Environments/";

    static readonly string[] TreeNames =
    {
        "SM_Env_Tree_01", "SM_Env_Tree_02", "SM_Env_Tree_03", "SM_Env_Tree_04", "SM_Env_Tree_05", "SM_Env_Tree_06",
        "SM_Env_TreePine_01", "SM_Env_TreePine_02", "SM_Env_TreePine_03",
    };

    [MenuItem("Tools/Zones/Add Synty Trees to Terrain")]
    public static void AddTrees()
    {
        var terrainGo = GameObject.Find("ZoneTerrain");
        var terrain = terrainGo != null ? terrainGo.GetComponent<Terrain>() : Object.FindFirstObjectByType<Terrain>();
        if (terrain == null)
        {
            Debug.LogWarning("[TreePalette] No Terrain found — run Tools/Zones/Build Terrain Zone first.");
            return;
        }

        var protos = new List<TreePrototype>();
        foreach (var n in TreeNames)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AdvEnv + n + ".prefab");
            if (prefab != null) protos.Add(new TreePrototype { prefab = prefab });
            else Debug.LogWarning($"[TreePalette] Missing tree prefab: {n}");
        }
        if (protos.Count == 0) { Debug.LogWarning("[TreePalette] No tree prefabs loaded."); return; }

        terrain.terrainData.treePrototypes = protos.ToArray();
        terrain.terrainData.RefreshPrototypes();

        Selection.activeGameObject = terrain.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[TreePalette] Registered {protos.Count} Synty tree(s) on '{terrain.name}'. NEXT: with the terrain " +
                  "selected → Terrain component → Paint Trees → pick a tree + brush to draw (or Terrain → Mass Place " +
                  "Trees for the whole zone). For collision: Terrain Settings → Enable Tree Colliders. Note: painted " +
                  "trees don't reliably carve the navmesh (runtime colliders) — use Tools/Zones/Scatter Props for " +
                  "boundary trees that must block mobs.");
    }

    /// <summary>
    /// Fixes painted trees floating above (or sunk into) the terrain surface. Unity bakes each
    /// <c>TreeInstance.position.y</c> in at paint time and never re-samples it — if the heightmap is reshaped
    /// afterward (as this zone's has been, repeatedly, via Build Terrain Zone), every tree painted before the
    /// last reshape stays at its stale height, reading as a uniform float. Re-snaps every existing tree instance
    /// to the CURRENT heightmap in one call via <c>TerrainData.SetTreeInstances(..., snapToHeightmap: true)</c> —
    /// safe to re-run any time after reshaping. Menu: <c>Tools/Zones/Snap Painted Trees to Terrain</c>.
    /// </summary>
    [MenuItem("Tools/Zones/Snap Painted Trees to Terrain")]
    public static void SnapTreesToTerrain()
    {
        var terrainGo = GameObject.Find("ZoneTerrain");
        var terrain = terrainGo != null ? terrainGo.GetComponent<Terrain>() : Object.FindFirstObjectByType<Terrain>();
        if (terrain == null)
        {
            Debug.LogWarning("[TreePalette] No Terrain found.");
            return;
        }

        var data = terrain.terrainData;
        int count = data.treeInstanceCount;
        if (count == 0)
        {
            Debug.LogWarning("[TreePalette] No painted tree instances on this terrain — nothing to snap.");
            return;
        }

        var instances = data.treeInstances; // copy
        data.SetTreeInstances(instances, true); // true = snapToHeightmap, re-samples every instance's Y
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"[TreePalette] Snapped {count} tree instance(s) on '{terrain.name}' to the current heightmap. " +
                  "If trees STILL float by the same amount after this, the cause is a mesh/pivot offset baked " +
                  "into the tree prefab itself (not a heightmap mismatch) — tell me and I'll look at fixing the " +
                  "prototype prefab instead.");
    }

    /// <summary>
    /// Fixes trees floating because the SOURCE MESH's own geometry doesn't touch its pivot at local Y=0 (the
    /// tree prefab's own Transform is untouched — confirmed via <c>SnapTreesToTerrain</c>'s snapToHeightmap not
    /// helping, which rules out a heightmap mismatch and leaves only the mesh itself as the remaining variable).
    /// For each registered tree prototype, measures the true lowest point of its mesh (accounting for any child
    /// transform offsets) and — if it's off the pivot — generates a corrected wrapper prefab under
    /// <c>Assets/Scenes/SampleScene/Terrain/GroundedTrees/</c> (never edits the vendored Synty asset) with the
    /// original model nested at a compensating offset, then swaps it into the SAME prototype slot so every
    /// already-painted instance (which references prototypes by index, not by prefab) picks it up automatically —
    /// no need to touch the 6000+ painted tree instances. Menu: <c>Tools/Zones/Fix Tree Pivot Offsets</c>.
    /// </summary>
    [MenuItem("Tools/Zones/Fix Tree Pivot Offsets")]
    public static void FixTreePivotOffsets()
    {
        var terrainGo = GameObject.Find("ZoneTerrain");
        var terrain = terrainGo != null ? terrainGo.GetComponent<Terrain>() : Object.FindFirstObjectByType<Terrain>();
        if (terrain == null)
        {
            Debug.LogWarning("[TreePalette] No Terrain found.");
            return;
        }

        var data = terrain.terrainData;
        var protos = data.treePrototypes;
        if (protos == null || protos.Length == 0)
        {
            Debug.LogWarning("[TreePalette] No tree prototypes registered — run Add Synty Trees to Terrain first.");
            return;
        }

        const string FixedDir = "Assets/Scenes/SampleScene/Terrain/GroundedTrees";
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/SampleScene/Terrain"))
            AssetDatabase.CreateFolder("Assets/Scenes/SampleScene", "Terrain");
        if (!AssetDatabase.IsValidFolder(FixedDir))
            AssetDatabase.CreateFolder("Assets/Scenes/SampleScene/Terrain", "GroundedTrees");

        const float Epsilon = 0.01f;
        var newProtos = new TreePrototype[protos.Length];
        int fixedCount = 0;

        for (int i = 0; i < protos.Length; i++)
        {
            var proto = protos[i];
            newProtos[i] = proto; // default: unchanged
            var prefab = proto.prefab;
            if (prefab == null) continue;

            float minY = MeasureLowestPoint(prefab);
            if (minY == float.MaxValue)
            {
                Debug.LogWarning($"[TreePalette] '{prefab.name}': no mesh found, skipped.");
                continue;
            }

            if (Mathf.Abs(minY) <= Epsilon)
            {
                Debug.Log($"[TreePalette] '{prefab.name}': already grounded (lowest point {minY:F4}u) — left alone.");
                continue;
            }

            string fixedPath = $"{FixedDir}/{prefab.name}_Grounded.prefab";
            var root = new GameObject(prefab.name + "_Grounded");
            var visual = Object.Instantiate(prefab, root.transform);
            visual.name = prefab.name;
            visual.transform.localPosition = new Vector3(0, -minY, 0);

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, fixedPath);
            Object.DestroyImmediate(root);

            newProtos[i] = new TreePrototype { prefab = savedPrefab };
            fixedCount++;
            Debug.Log($"[TreePalette] '{prefab.name}': lowest mesh point was {minY:F4}u off its pivot → " +
                      $"created grounded wrapper at {fixedPath}.");
        }

        if (fixedCount == 0)
        {
            Debug.Log("[TreePalette] Every registered tree prototype's mesh already touches its pivot at Y=0 — " +
                      "no offset found. The float must have a different cause; tell me exactly what you're seeing " +
                      "(all trees the same amount? one species only? does it vary by terrain height?).");
            return;
        }

        data.treePrototypes = newProtos;
        data.RefreshPrototypes();
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"[TreePalette] Fixed {fixedCount} of {protos.Length} tree prototype(s). Existing painted " +
                  "instances are unaffected (they reference prototypes by index, not by prefab) — trees should " +
                  "now sit flush without repainting anything. Save the scene.");
    }

    static float MeasureLowestPoint(GameObject prefab)
    {
        float minY = float.MaxValue;
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var localToRoot = LocalToRoot(mf.transform, prefab.transform);
            var b = mf.sharedMesh.bounds;
            foreach (var corner in BoundsCorners(b))
            {
                float y = localToRoot.MultiplyPoint3x4(corner).y;
                if (y < minY) minY = y;
            }
        }
        return minY;
    }

    static Matrix4x4 LocalToRoot(Transform t, Transform root)
    {
        var m = Matrix4x4.identity;
        var cur = t;
        while (cur != null && cur != root)
        {
            m = Matrix4x4.TRS(cur.localPosition, cur.localRotation, cur.localScale) * m;
            cur = cur.parent;
        }
        return m;
    }

    static IEnumerable<Vector3> BoundsCorners(Bounds b)
    {
        var mn = b.min; var mx = b.max;
        yield return new Vector3(mn.x, mn.y, mn.z);
        yield return new Vector3(mn.x, mn.y, mx.z);
        yield return new Vector3(mn.x, mx.y, mn.z);
        yield return new Vector3(mn.x, mx.y, mx.z);
        yield return new Vector3(mx.x, mn.y, mn.z);
        yield return new Vector3(mx.x, mn.y, mx.z);
        yield return new Vector3(mx.x, mx.y, mn.z);
        yield return new Vector3(mx.x, mx.y, mx.z);
    }

    /// <summary>
    /// Ground-truth diagnostic: for every painted tree instance, raycasts straight down onto the TERRAIN
    /// COLLIDER specifically (same surface a player's CharacterController stands on) at that tree's exact
    /// (x,z), and compares it against the tree's stored world Y. Both SnapTreesToTerrain (heightmap match)
    /// and FixTreePivotOffsets (mesh pivot) have already been ruled out by hard measurement — this settles
    /// whether there's a genuine numeric gap left at all, or whether what's visible is a rendering/shading
    /// illusion (contact shadow, or terrain LOD/Pixel-Error simplification at distance) with no real gap to
    /// fix. Menu: <c>Tools/Zones/Diagnose Tree Float</c>.
    /// </summary>
    [MenuItem("Tools/Zones/Diagnose Tree Float")]
    public static void DiagnoseTreeFloat()
    {
        var terrainGo = GameObject.Find("ZoneTerrain");
        var terrain = terrainGo != null ? terrainGo.GetComponent<Terrain>() : Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) { Debug.LogWarning("[TreePalette] No Terrain found."); return; }

        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider == null)
        {
            Debug.LogWarning("[TreePalette] Terrain has no TerrainCollider — can't measure ground truth.");
            return;
        }

        var data = terrain.terrainData;
        var instances = data.treeInstances;
        if (instances.Length == 0) { Debug.LogWarning("[TreePalette] No tree instances."); return; }

        var tPos = terrain.transform.position;
        float rayTop = tPos.y + data.size.y + 200f;
        float rayLen = data.size.y + 400f;

        float maxAbove = float.MinValue, maxBelow = float.MaxValue;
        int aboveIdx = -1, belowIdx = -1;
        double sumAbs = 0;
        int hitCount = 0, missCount = 0;

        for (int i = 0; i < instances.Length; i++)
        {
            var inst = instances[i];
            float worldX = tPos.x + inst.position.x * data.size.x;
            float worldZ = tPos.z + inst.position.z * data.size.z;
            float worldYTree = tPos.y + inst.position.y * data.size.y;

            var ray = new Ray(new Vector3(worldX, rayTop, worldZ), Vector3.down);
            if (collider.Raycast(ray, out var hit, rayLen))
            {
                float delta = worldYTree - hit.point.y; // positive = tree sits above the terrain collider
                sumAbs += System.Math.Abs(delta);
                hitCount++;
                if (delta > maxAbove) { maxAbove = delta; aboveIdx = i; }
                if (delta < maxBelow) { maxBelow = delta; belowIdx = i; }
            }
            else missCount++;
        }

        if (hitCount == 0)
        {
            Debug.LogWarning("[TreePalette] No raycasts hit the TerrainCollider — nothing to report.");
            return;
        }

        Debug.Log($"[TreePalette] Diagnosed {hitCount} tree instance(s) against the TerrainCollider " +
                  $"({missCount} miss(es)).\n" +
                  $"  Average |delta| = {(sumAbs / hitCount):F4}u\n" +
                  $"  Max ABOVE ground = {maxAbove:F4}u (instance #{aboveIdx}, prototype {instances[aboveIdx].prototypeIndex})\n" +
                  $"  Max BELOW ground = {maxBelow:F4}u (instance #{belowIdx}, prototype {instances[belowIdx].prototypeIndex})\n" +
                  "Near 0 across the board = trees are numerically correct; what you're seeing is a rendering " +
                  "illusion (contact shadow / terrain LOD-Pixel-Error at distance), not a real gap — nothing to " +
                  "fix in data. Consistently positive by a real amount (e.g. > 0.05u) = a genuine gap, and this " +
                  "number tells me exactly how much to correct.");
    }

    /// <summary>
    /// Opens a small window to randomize height/width scale on every already-painted tree instance IN PLACE —
    /// no repainting needed. Each run assigns a fresh random value per instance within the chosen range (not a
    /// compounding multiply), so it's safe to re-run/retune. Since every tree mesh's pivot sits at its base
    /// (confirmed by FixTreePivotOffsets), scaling around that pivot only grows/shrinks the canopy upward — the
    /// base stays planted, so this can't reintroduce floating. Menu: <c>Tools/Zones/Randomize Painted Tree Sizes...</c>.
    /// </summary>
    [MenuItem("Tools/Zones/Randomize Painted Tree Sizes...")]
    public static void OpenRandomizeSizesWindow() => TreeSizeRandomizerWindow.ShowWindow();

    public static int ApplyRandomSizes(float minHeight, float maxHeight, float minWidth, float maxWidth, bool lockWidthToHeight)
    {
        var terrainGo = GameObject.Find("ZoneTerrain");
        var terrain = terrainGo != null ? terrainGo.GetComponent<Terrain>() : Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) { Debug.LogWarning("[TreePalette] No Terrain found."); return 0; }

        var data = terrain.terrainData;
        var instances = data.treeInstances;
        if (instances.Length == 0) { Debug.LogWarning("[TreePalette] No tree instances to randomize."); return 0; }

        for (int i = 0; i < instances.Length; i++)
        {
            var inst = instances[i];
            inst.heightScale = Random.Range(minHeight, maxHeight);
            inst.widthScale = lockWidthToHeight ? inst.heightScale : Random.Range(minWidth, maxWidth);
            instances[i] = inst;
        }

        data.SetTreeInstances(instances, true); // true = also re-snap Y to heightmap, belt-and-braces
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"[TreePalette] Randomized size on {instances.Length} tree instance(s) — height " +
                  $"[{minHeight:F2}-{maxHeight:F2}], width " +
                  (lockWidthToHeight ? "locked to height" : $"[{minWidth:F2}-{maxWidth:F2}]") +
                  ". Save the scene.");
        return instances.Length;
    }
}

class TreeSizeRandomizerWindow : EditorWindow
{
    float _minHeight = 0.8f, _maxHeight = 1.2f;
    float _minWidth = 0.8f, _maxWidth = 1.2f;
    bool _lockWidthToHeight = true;

    public static void ShowWindow()
    {
        var w = GetWindow<TreeSizeRandomizerWindow>(true, "Randomize Tree Sizes");
        w.minSize = new Vector2(320, 200);
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Randomizes height/width on every already-painted tree instance in place — no repainting needed. " +
            "Safe to re-run with different values.", MessageType.Info);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Height Scale Range", EditorStyles.boldLabel);
        _minHeight = EditorGUILayout.FloatField("Min", _minHeight);
        _maxHeight = EditorGUILayout.FloatField("Max", _maxHeight);

        EditorGUILayout.Space();
        _lockWidthToHeight = EditorGUILayout.Toggle("Lock Width to Height", _lockWidthToHeight);
        using (new EditorGUI.DisabledScope(_lockWidthToHeight))
        {
            EditorGUILayout.LabelField("Width Scale Range", EditorStyles.boldLabel);
            _minWidth = EditorGUILayout.FloatField("Min", _minWidth);
            _maxWidth = EditorGUILayout.FloatField("Max", _maxWidth);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Apply to All Painted Trees", GUILayout.Height(30)))
        {
            _minHeight = Mathf.Max(0.05f, _minHeight);
            _maxHeight = Mathf.Max(_minHeight, _maxHeight);
            _minWidth = Mathf.Max(0.05f, _minWidth);
            _maxWidth = Mathf.Max(_minWidth, _maxWidth);
            TerrainTreePalette.ApplyRandomSizes(_minHeight, _maxHeight, _minWidth, _maxWidth, _lockWidthToHeight);
        }
    }
}
