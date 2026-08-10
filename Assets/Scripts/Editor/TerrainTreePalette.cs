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
    /// Read-only diagnostic for the theory that survives after both placement (DiagnoseTreeFloat: ~0.0000u
    /// against the real TerrainCollider) and pivot (FixTreePivotOffsets: mesh already touches Y=0) come back
    /// clean, yet trees still visibly float even on flat ground with shadows/SSAO both off: the mesh's lowest
    /// VERTEX can sit exactly on the ground while the trunk isn't flat-bottomed (tapered/pointed for polycount,
    /// common on low-poly stylized trees) — so the visible "ring" a player reads as the trunk base sits well
    /// above that single lowest point, and no amount of correct PLACEMENT data can fix a mesh SHAPE gap. Reports
    /// the widest XZ radius found within several height bands above the lowest vertex, so you can see exactly
    /// where the taper stabilizes into "the trunk you actually see" — that height is the embed depth to use.
    /// Menu: <c>Tools/Zones/Measure Tree Base Taper</c>.
    /// </summary>
    [MenuItem("Tools/Zones/Measure Tree Base Taper")]
    public static void MeasureTreeBaseTaper()
    {
        var terrainGo = GameObject.Find("ZoneTerrain");
        var terrain = terrainGo != null ? terrainGo.GetComponent<Terrain>() : Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) { Debug.LogWarning("[TreePalette] No Terrain found."); return; }

        var protos = terrain.terrainData.treePrototypes;
        if (protos == null || protos.Length == 0)
        {
            Debug.LogWarning("[TreePalette] No tree prototypes registered — run Add Synty Trees to Terrain first.");
            return;
        }

        var seen = new HashSet<string>();
        foreach (var proto in protos)
        {
            if (proto.prefab == null) continue;
            var original = LoadOriginalTreePrefab(proto.prefab.name) ?? proto.prefab;
            if (!seen.Add(original.name)) continue; // report each distinct species once
            ReportBaseTaper(original);
        }
    }

    static readonly float[] TaperBands = { 0.00f, 0.05f, 0.10f, 0.15f, 0.20f, 0.30f, 0.50f };

    static void ReportBaseTaper(GameObject prefab)
    {
        float minY = float.MaxValue;
        var verts = new List<Vector3>();
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            if (!mf.sharedMesh.isReadable)
            {
                Debug.LogWarning($"[TreePalette] '{prefab.name}': mesh '{mf.sharedMesh.name}' isn't Read/Write " +
                                  "Enabled, can't inspect its vertices. Select the mesh's source model in the " +
                                  "Project window → Inspector → Model tab → check Read/Write Enabled → Apply, " +
                                  "then re-run this.");
                continue;
            }
            var localToRoot = LocalToRoot(mf.transform, prefab.transform);
            foreach (var v in mf.sharedMesh.vertices)
            {
                var p = localToRoot.MultiplyPoint3x4(v);
                verts.Add(p);
                if (p.y < minY) minY = p.y;
            }
        }
        if (verts.Count == 0) { Debug.LogWarning($"[TreePalette] '{prefab.name}': no vertices found."); return; }

        var sb = new System.Text.StringBuilder();
        sb.Append($"[TreePalette] '{prefab.name}' base taper (lowest vertex at local Y={minY:F4}, {verts.Count} verts total):\n");
        foreach (var band in TaperBands)
        {
            float yThreshold = minY + band;
            float maxRadius = 0f;
            int count = 0;
            foreach (var p in verts)
            {
                if (p.y > yThreshold) continue;
                float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                if (r > maxRadius) maxRadius = r;
                count++;
            }
            sb.Append($"  <= {band:F2}u above lowest point: {count,4} vert(s), widest radius {maxRadius:F4}u\n");
        }
        sb.Append("If the radius stays tiny near the bottom then jumps to something close to the trunk's real " +
                   "width a fair bit higher, that band height is roughly how deep the mesh needs to be embedded " +
                   "for the VISIBLE trunk (not just the lowest point) to read as grounded.");
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// The real fix, confirmed by a screenshot showing a genuine dark void under a trunk's base: placement is
    /// exact at each tree's single center pivot (DiagnoseTreeFloat: ~0.0000u), and the mesh isn't tapered
    /// (MeasureTreeBaseTaper: a flat ~0.5u-radius base ring) — but NEITHER check ever tested the EDGE of that
    /// disk. This terrain's heightmap resolution was deliberately dropped for the chunky low-poly look (~11.7u
    /// triangular facets), so almost no facet is perfectly level — even ones that read as "flat" to a walking
    /// player. Over a half-meter-plus trunk radius, a mild facet tilt is enough to open a real gap on the
    /// downhill edge while the center (all any prior check looked at) stays perfectly seated.
    ///
    /// A single flat embed depth applied to every tree (see ApplyEmbedDepth below) is the wrong shape of fix for
    /// this — it'd be insufficient on tilted facets and needlessly deep on genuinely flat ones. This is instead
    /// PER-INSTANCE and self-adaptive: for each painted tree, samples the terrain height at several points around
    /// a circle matching that species' measured base radius (scaled by the instance's own widthScale) centered
    /// on the tree, and — only if the lowest sample found is BELOW the tree's current height — lowers the
    /// instance to that worst-case height plus a small safety margin, so the entire visible base ring sits at or
    /// below the true local ground everywhere, not just its center. Flat spots barely move; tilted ones get
    /// exactly what they need. Never raises a tree, only sinks — safe to re-run any time (e.g. after reshaping
    /// terrain again). Menu: <c>Tools/Zones/Ground Trees to Local Terrain (Adaptive)</c>.
    /// </summary>
    [MenuItem("Tools/Zones/Ground Trees to Local Terrain (Adaptive)")]
    public static void GroundTreesAdaptive()
    {
        var terrainGo = GameObject.Find("ZoneTerrain");
        var terrain = terrainGo != null ? terrainGo.GetComponent<Terrain>() : Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) { Debug.LogWarning("[TreePalette] No Terrain found."); return; }

        var data = terrain.terrainData;
        var protos = data.treePrototypes;
        var instances = data.treeInstances;
        if (protos == null || protos.Length == 0)
        {
            Debug.LogWarning("[TreePalette] No tree prototypes registered — run Add Synty Trees to Terrain first.");
            return;
        }
        if (instances.Length == 0) { Debug.LogWarning("[TreePalette] No tree instances."); return; }

        const int SampleCount = 12;
        const float SafetyMargin = 0.02f;

        var radiusByPrototype = new float[protos.Length];
        for (int i = 0; i < protos.Length; i++)
        {
            var prefab = protos[i].prefab;
            if (prefab == null) { radiusByPrototype[i] = 0f; continue; }
            var original = LoadOriginalTreePrefab(prefab.name) ?? prefab;
            radiusByPrototype[i] = MeasureBaseRadius(original);
        }

        var tPos = terrain.transform.position;
        int sunkCount = 0;
        double maxSink = 0;

        for (int i = 0; i < instances.Length; i++)
        {
            var inst = instances[i];
            if (inst.prototypeIndex < 0 || inst.prototypeIndex >= radiusByPrototype.Length) continue;
            float radius = radiusByPrototype[inst.prototypeIndex] * Mathf.Max(inst.widthScale, 0.01f);
            if (radius <= 0f) continue;

            float worldX = tPos.x + inst.position.x * data.size.x;
            float worldZ = tPos.z + inst.position.z * data.size.z;
            float currentWorldY = tPos.y + inst.position.y * data.size.y;

            float minHeight = terrain.SampleHeight(new Vector3(worldX, 0, worldZ)) + tPos.y;
            for (int s = 0; s < SampleCount; s++)
            {
                float ang = (s / (float)SampleCount) * Mathf.PI * 2f;
                float sx = worldX + Mathf.Cos(ang) * radius;
                float sz = worldZ + Mathf.Sin(ang) * radius;
                float h = terrain.SampleHeight(new Vector3(sx, 0, sz)) + tPos.y;
                if (h < minHeight) minHeight = h;
            }

            float targetWorldY = minHeight - SafetyMargin;
            if (targetWorldY < currentWorldY - 0.0005f) // only sink; skip negligible no-op changes
            {
                double sinkAmount = currentWorldY - targetWorldY;
                if (sinkAmount > maxSink) maxSink = sinkAmount;
                inst.position.y = (targetWorldY - tPos.y) / data.size.y;
                instances[i] = inst;
                sunkCount++;
            }
        }

        data.SetTreeInstances(instances, false); // false: we already computed exact target heights ourselves
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"[TreePalette] Adaptively grounded {sunkCount} of {instances.Length} tree instance(s) — each " +
                  $"sunk only as much as ITS OWN local terrain required (largest sink applied: {maxSink:F4}u). " +
                  "Flat spots barely moved; tilted ones got exactly enough. Safe to re-run any time — only ever " +
                  "sinks further if needed, never raises a tree back up.");
    }

    static float MeasureBaseRadius(GameObject prefab)
    {
        float minY = float.MaxValue;
        var verts = new List<Vector3>();
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            var localToRoot = LocalToRoot(mf.transform, prefab.transform);
            foreach (var v in mf.sharedMesh.vertices)
            {
                var p = localToRoot.MultiplyPoint3x4(v);
                verts.Add(p);
                if (p.y < minY) minY = p.y;
            }
        }
        if (verts.Count == 0) return 0f;

        const float Band = 0.05f;
        float maxRadius = 0f;
        foreach (var p in verts)
        {
            if (p.y > minY + Band) continue;
            float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
            if (r > maxRadius) maxRadius = r;
        }
        return maxRadius;
    }

    /// <summary>
    /// SECONDARY option — <c>GroundTreesAdaptive</c> above (Tools/Zones/Ground Trees to Local Terrain (Adaptive))
    /// is the correct per-instance fix and should be tried first; this applies one FLAT depth to every tree
    /// regardless of its local terrain, which is either insufficient on tilted spots or excessive on flat ones.
    /// Still useful as a small uniform "bury the roots a bit" stylistic pass on top of the adaptive fix, or as a
    /// quick blunt-force option. Fixes trees floating on SLOPED ground — a different problem than
    /// <c>FixTreePivotOffsets</c> (a raw mesh/pivot authoring bug, already ruled out — every prototype's mesh
    /// touches its pivot at Y=0) and <c>DiagnoseTreeFloat</c> (a ground-truth raycast at the tree's exact
    /// placement point, which reported ~0 delta and always will: only that ONE point is guaranteed flush). The
    /// trunk mesh has real width, and
    /// on a sloped terrain cell the ground drops away under the downhill edge of that footprint — the gap grows
    /// with slope × trunk radius, which is exactly "floats more where the terrain is steeper." Fixing it means
    /// sinking the whole mesh a bit below flush so the downhill gap stays hidden under the surface (the uphill
    /// edge just buries a little deeper — standard trick, real games always partially bury trunks). Always
    /// regenerates from the ORIGINAL vendored prefab (not whatever's currently swapped into the slot), so it's
    /// safe to re-run repeatedly with a different depth while tuning. Swaps into the SAME prototype slots —
    /// existing painted instances update immediately (they reference prototypes by index, not by prefab), no
    /// repaint required. Menu: <c>Tools/Zones/Embed Trees Into Ground...</c>.
    /// </summary>
    [MenuItem("Tools/Zones/Embed Trees Into Ground...")]
    public static void OpenEmbedTreesWindow() => TreeEmbedWindow.ShowWindow();

    public static int ApplyEmbedDepth(float embedDepth)
    {
        var terrainGo = GameObject.Find("ZoneTerrain");
        var terrain = terrainGo != null ? terrainGo.GetComponent<Terrain>() : Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) { Debug.LogWarning("[TreePalette] No Terrain found."); return 0; }

        var data = terrain.terrainData;
        var protos = data.treePrototypes;
        if (protos == null || protos.Length == 0)
        {
            Debug.LogWarning("[TreePalette] No tree prototypes registered — run Add Synty Trees to Terrain first.");
            return 0;
        }

        const string EmbedDir = "Assets/Scenes/SampleScene/Terrain/EmbeddedTrees";
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/SampleScene/Terrain"))
            AssetDatabase.CreateFolder("Assets/Scenes/SampleScene", "Terrain");
        if (!AssetDatabase.IsValidFolder(EmbedDir))
            AssetDatabase.CreateFolder("Assets/Scenes/SampleScene/Terrain", "EmbeddedTrees");

        var newProtos = new TreePrototype[protos.Length];
        int fixedCount = 0;

        for (int i = 0; i < protos.Length; i++)
        {
            var proto = protos[i];
            newProtos[i] = proto;
            if (proto.prefab == null) continue;

            var original = LoadOriginalTreePrefab(proto.prefab.name) ?? proto.prefab;
            float minY = MeasureLowestPoint(original);
            if (minY == float.MaxValue)
            {
                Debug.LogWarning($"[TreePalette] '{original.name}': no mesh found, skipped.");
                continue;
            }

            string fixedPath = $"{EmbedDir}/{original.name}_Embedded.prefab";
            var root = new GameObject(original.name + "_Embedded");
            var visual = Object.Instantiate(original, root.transform);
            visual.name = original.name;
            visual.transform.localPosition = new Vector3(0, -minY - embedDepth, 0);

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, fixedPath);
            Object.DestroyImmediate(root);

            newProtos[i] = new TreePrototype { prefab = savedPrefab };
            fixedCount++;
        }

        data.treePrototypes = newProtos;
        data.RefreshPrototypes();
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"[TreePalette] Embedded {fixedCount} of {protos.Length} tree prototype(s) {embedDepth:F2}u into " +
                  "the ground. Existing painted instances update immediately (prototypes referenced by index) — " +
                  "no repaint needed, though repainting fresh on top is harmless if you want a clean pass. Heads " +
                  "up: instances with a randomized height/width scale (if you ran Randomize Painted Tree Sizes) " +
                  "will scale this offset proportionally too, so the effective depth varies a little per instance " +
                  "— usually imperceptible. If the tallest/steepest spots still float, bump the depth and re-run " +
                  "(each run re-sources the ORIGINAL model, so it's safe to iterate).");
        return fixedCount;
    }

    /// <summary>
    /// Resolves the pristine vendored source prefab for a (possibly already-wrapped) prototype prefab name, so
    /// repeated tool runs always measure/offset from the original mesh instead of compounding on a prior wrapper.
    /// </summary>
    static GameObject LoadOriginalTreePrefab(string protoPrefabName)
    {
        string baseName = protoPrefabName;
        foreach (var suffix in new[] { "_Embedded", "_Grounded" })
        {
            if (baseName.EndsWith(suffix))
            {
                baseName = baseName.Substring(0, baseName.Length - suffix.Length);
                break;
            }
        }
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(AdvEnv + baseName + ".prefab");
        return original != null ? original : AssetDatabase.LoadAssetAtPath<GameObject>(AdvEnv + protoPrefabName + ".prefab");
    }

    /// <summary>
    /// Measures + stores per-species trunk radius/height on a runtime <see cref="TreeColliderGenerator"/>
    /// component (added to the terrain if missing) so painted trees get real, always-present trunk collision —
    /// see that class's doc comment for why this exists instead of Unity's built-in "Create Tree Colliders"
    /// (camera-relative generation that doesn't reliably work on a dedicated server). This is a ONE-TIME editor
    /// measurement, not a snapshot of the current layout — the generated colliders themselves are built fresh
    /// from whatever the CURRENT painted layout is every time the scene loads, so repainting/moving/adding more
    /// of an EXISTING species with the brush needs nothing further. Only re-run this if you register a genuinely
    /// NEW tree species on the palette (Add Synty Trees to Terrain), so its radius/height gets measured too.
    /// Menu: <c>Tools/Zones/Generate Tree Collider Profiles</c>.
    /// </summary>
    [MenuItem("Tools/Zones/Generate Tree Collider Profiles")]
    public static void GenerateTreeColliderProfiles()
    {
        var terrainGo = GameObject.Find("ZoneTerrain");
        var terrain = terrainGo != null ? terrainGo.GetComponent<Terrain>() : Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) { Debug.LogWarning("[TreePalette] No Terrain found."); return; }

        var protos = terrain.terrainData.treePrototypes;
        if (protos == null || protos.Length == 0)
        {
            Debug.LogWarning("[TreePalette] No tree prototypes registered — run Add Synty Trees to Terrain first.");
            return;
        }

        var generator = terrain.GetComponent<TreeColliderGenerator>();
        if (generator == null) generator = terrain.gameObject.AddComponent<TreeColliderGenerator>();

        var profiles = new List<TreeColliderProfile>();
        var seen = new HashSet<string>();
        foreach (var proto in protos)
        {
            if (proto.prefab == null) continue;
            var original = LoadOriginalTreePrefab(proto.prefab.name) ?? proto.prefab;
            if (!seen.Add(original.name)) continue;

            float radius = MeasureBaseRadius(original);
            float height = MeasureTrunkHeight(original, radius);
            profiles.Add(new TreeColliderProfile { prefabName = original.name, radius = radius, height = height });
        }

        generator.SetProfiles(profiles);
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        var summary = new System.Text.StringBuilder();
        foreach (var p in profiles) summary.Append($"\n  {p.prefabName}: radius {p.radius:F3}u, trunk height {p.height:F3}u");
        Debug.Log($"[TreePalette] Measured {profiles.Count} tree collider profile(s) on '{terrain.name}':{summary}\n" +
                  "Save the scene. Trunk colliders now generate automatically every time this terrain loads, " +
                  "matching whatever the CURRENT painted layout is — paint/move/delete freely with the brush, " +
                  "nothing further to run for these same species.");
    }

    /// <summary>
    /// Finds the height above a mesh's lowest vertex at which its cross-sectional radius grows substantially
    /// past the measured base radius — i.e. roughly where the trunk ends and canopy/branches begin. Used to cap
    /// a generated trunk collider's height so it never extends up into low-hanging foliage.
    /// </summary>
    static float MeasureTrunkHeight(GameObject prefab, float baseRadius)
    {
        float minY = float.MaxValue;
        var verts = new List<Vector3>();
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            var localToRoot = LocalToRoot(mf.transform, prefab.transform);
            foreach (var v in mf.sharedMesh.vertices)
            {
                var p = localToRoot.MultiplyPoint3x4(v);
                verts.Add(p);
                if (p.y < minY) minY = p.y;
            }
        }
        if (verts.Count == 0 || baseRadius <= 0f) return 1.5f; // sane fallback if unmeasurable

        const float GrowthFactor = 1.5f;   // canopy = radius grows past 1.5x the trunk's base radius
        const float MaxSearchHeight = 4f;  // don't scan absurdly tall trees looking for this
        const float StepSize = 0.05f;

        for (float h = StepSize; h <= MaxSearchHeight; h += StepSize)
        {
            float cumMaxRadius = 0f;
            foreach (var p in verts)
            {
                if (p.y > minY + h) continue;
                float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                if (r > cumMaxRadius) cumMaxRadius = r;
            }
            if (cumMaxRadius > baseRadius * GrowthFactor)
                return Mathf.Max(h - StepSize, 0.3f);
        }
        return MaxSearchHeight; // never widened within the search range — trunk-like the whole way
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

class TreeEmbedWindow : EditorWindow
{
    float _embedDepth = 0.3f;

    public static void ShowWindow()
    {
        var w = GetWindow<TreeEmbedWindow>(true, "Embed Trees Into Ground");
        w.minSize = new Vector2(380, 220);
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Only a tree's exact placement point is guaranteed to sit flush on the terrain — the trunk mesh has " +
            "real width, so on sloped ground the downhill edge of its base floats above the surface (worse on " +
            "steeper terrain). Sinking the whole mesh below flush hides that gap. Regenerates prototype prefabs " +
            "from the original vendored models and swaps them into the terrain's tree prototype slots — already-" +
            "painted trees update immediately, no repaint needed. Safe to re-run with a different depth.",
            MessageType.Info);
        EditorGUILayout.Space();

        _embedDepth = EditorGUILayout.Slider("Embed Depth (u)", _embedDepth, 0f, 1.5f);

        EditorGUILayout.Space();
        if (GUILayout.Button("Apply to All Tree Prototypes", GUILayout.Height(30)))
        {
            TerrainTreePalette.ApplyEmbedDepth(Mathf.Max(0f, _embedDepth));
        }
    }
}
