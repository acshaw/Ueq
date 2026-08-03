using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drops a fenced farmland plot (flattened ground, crop rows, perimeter fence + gate, optional scarecrow/
/// windmill) at the Scene view pivot, built entirely from the POLYGON Nature Biomes (Meadow/Forest) farm kit
/// you already own. Same in-place terrain-editing discipline as <see cref="CoastlinePeninsulaTool"/> — partial
/// GetHeights/SetHeights + GetAlphamaps/SetAlphamaps on the LIVE TerrainData, never a full regenerate — so
/// it's safe to run on an already-decorated field. Re-runnable per plot: move the Scene view over open ground
/// and run again for each of your 3-4 fields.
///
/// Menu: <c>Tools/Zones/Build Farmland Plot</c>.
/// </summary>
public class FarmlandPlotTool : EditorWindow
{
    const string Farm = "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/Props/";
    const string FarmRoot = "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/";

    static readonly string[] CropClumps =
    {
        FarmRoot + "SM_Env_CropField_Clump_01.prefab",
        FarmRoot + "SM_Env_CropField_Clump_02.prefab",
    };
    static readonly string[] FenceStyles =
    {
        Farm + "SM_Prop_Meadow_Fence_01.prefab",
        Farm + "SM_Prop_Meadow_Fence_02.prefab",
        Farm + "SM_Prop_Meadow_Fence_03.prefab",
    };

    Terrain _terrain;
    string _plotName = "Farmland Plot";
    float _width = 45f;
    float _depth = 32f;
    bool _flatten = true;
    bool _clearTrees = true;
    float _cropInset = 4f;
    float _cropSpacing = 4.5f;
    float _cropJitter = 0.8f;
    bool _includeFence = true;
    int _fenceStyle;
    bool _includeScarecrow = true;
    bool _includeWindmill;

    [MenuItem("Tools/Zones/Build Farmland Plot")]
    static void Open()
    {
        var w = GetWindow<FarmlandPlotTool>("Farmland Plot");
        w.minSize = new Vector2(340, 440);
        w.Refresh();
    }

    void OnFocus() => Refresh();
    void Refresh()
    {
        var go = GameObject.Find("ZoneTerrain");
        _terrain = go != null ? go.GetComponent<Terrain>() : FindFirstObjectByType<Terrain>();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Edits the live terrain in place (safe for painted trees). Position the Scene view over open " +
            "ground first — the plot centers on the Scene view pivot, snapped to the surface.",
            MessageType.Info);

        _terrain = (Terrain)EditorGUILayout.ObjectField("Target terrain", _terrain, typeof(Terrain), true);
        if (_terrain == null)
        {
            EditorGUILayout.HelpBox("No terrain found. Run Tools/Zones/Build Terrain Zone first.", MessageType.Warning);
            return;
        }

        _plotName = EditorGUILayout.TextField("Plot name", _plotName);
        _width = EditorGUILayout.Slider("Width (u, east-west)", _width, 15f, 100f);
        _depth = EditorGUILayout.Slider("Depth (u, north-south)", _depth, 15f, 100f);

        EditorGUILayout.Space();
        _flatten = EditorGUILayout.Toggle("Flatten ground under plot", _flatten);
        _clearTrees = EditorGUILayout.Toggle("Clear trees inside footprint", _clearTrees);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Crops", EditorStyles.boldLabel);
        _cropInset = EditorGUILayout.Slider("Inset from fence (u)", _cropInset, 1f, 10f);
        _cropSpacing = EditorGUILayout.Slider("Row spacing (u)", _cropSpacing, 2.5f, 10f);
        _cropJitter = EditorGUILayout.Slider("Jitter", _cropJitter, 0f, 2f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dressing", EditorStyles.boldLabel);
        _includeFence = EditorGUILayout.Toggle("Perimeter fence + gate", _includeFence);
        if (_includeFence)
            _fenceStyle = EditorGUILayout.Popup("Fence style", _fenceStyle, new[] { "Style 1", "Style 2", "Style 3" });
        _includeScarecrow = EditorGUILayout.Toggle("Scarecrow", _includeScarecrow);
        _includeWindmill = EditorGUILayout.Toggle("Windmill (use sparingly — one per field)", _includeWindmill);

        EditorGUILayout.Space();
        if (GUILayout.Button("Build Plot At Scene View Pivot", GUILayout.Height(30)))
            Build();
    }

    void Build()
    {
        Vector3 pivot = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        Vector3 corner = _terrain.transform.position;
        var td = _terrain.terrainData;
        float fieldWidth = td.size.x, fieldLength = td.size.z;

        Vector3 center = new(pivot.x, 0f, pivot.z);
        float xMin = center.x - _width * 0.5f, xMax = center.x + _width * 0.5f;
        float zMin = center.z - _depth * 0.5f, zMax = center.z + _depth * 0.5f;

        if (_flatten) FlattenFootprint(td, corner, fieldWidth, fieldLength, xMin, xMax, zMin, zMax);
        if (_clearTrees) ClearTreesInFootprint(td, corner, fieldWidth, fieldLength, xMin, xMax, zMin, zMax);
        RepaintGrass(td, corner, fieldWidth, fieldLength, xMin - 4f, xMax + 4f, zMin - 4f, zMax + 4f);
        _terrain.Flush();

        float centerY = TerrainShapingUtil.SurfaceY(_terrain, corner, fieldWidth, fieldLength, center.x, center.z);
        var root = new GameObject(_plotName).transform;
        root.position = new Vector3(center.x, centerY, center.z);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Farmland Plot");

        var rng = new System.Random(_plotName.GetHashCode() ^ Mathf.RoundToInt(center.x * 13f) ^ Mathf.RoundToInt(center.z * 7f));

        if (_includeFence) BuildFence(root, xMin, xMax, zMin, zMax);

        BuildCrops(root, xMin, xMax, zMin, zMax, rng);

        if (_includeScarecrow)
        {
            var scare = AssetDatabase.LoadAssetAtPath<GameObject>(Farm + "SM_Prop_ScareCrow_01.prefab");
            if (scare != null)
                PlaceOnSurface(root, scare, center.x + (float)(rng.NextDouble() * 6 - 3), center.z + (float)(rng.NextDouble() * 6 - 3), rng, 0.1f);
        }
        if (_includeWindmill)
        {
            var mill = AssetDatabase.LoadAssetAtPath<GameObject>(FarmRoot + "SM_Bld_Windmill_01.prefab");
            if (mill != null)
                PlaceOnSurface(root, mill, xMax + 8f, center.z, rng, 0f);
        }

        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[FarmlandPlot] Built '{_plotName}' ({_width:F0}x{_depth:F0}u) at {root.position}. " +
                  "NEXT: rebake the navmesh (Tools/Terrain/Rebake NavMesh) — the fence posts carve it.");
    }

    // ── Terrain editing (in place) ────────────────────────────────────────────────────────────────────
    void FlattenFootprint(TerrainData td, Vector3 corner, float fieldWidth, float fieldLength,
        float xMin, float xMax, float zMin, float zMax)
    {
        const float falloff = 6f;
        var (hxBase, hyBase, hw, hh) = ClampRegion(corner, fieldWidth, fieldLength, td.heightmapResolution,
            xMin - falloff, xMax + falloff, zMin - falloff, zMax + falloff);
        if (hw <= 0 || hh <= 0) return;

        var heights = td.GetHeights(hxBase, hyBase, hw, hh);
        int res = td.heightmapResolution;

        float sum = 0f; int count = 0;
        for (int zi = 0; zi < hh; zi++)
        {
            float worldZ = corner.z + (hyBase + zi) / (float)(res - 1) * fieldLength;
            if (worldZ < zMin || worldZ > zMax) continue;
            for (int xi = 0; xi < hw; xi++)
            {
                float worldX = corner.x + (hxBase + xi) / (float)(res - 1) * fieldWidth;
                if (worldX < xMin || worldX > xMax) continue;
                sum += heights[zi, xi]; count++;
            }
        }
        if (count == 0) return;
        float target = sum / count;

        for (int zi = 0; zi < hh; zi++)
        {
            float worldZ = corner.z + (hyBase + zi) / (float)(res - 1) * fieldLength;
            for (int xi = 0; xi < hw; xi++)
            {
                float worldX = corner.x + (hxBase + xi) / (float)(res - 1) * fieldWidth;
                float dx = Mathf.Max(0f, Mathf.Max(xMin - worldX, worldX - xMax));
                float dz = Mathf.Max(0f, Mathf.Max(zMin - worldZ, worldZ - zMax));
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                float t = 1f - Mathf.Clamp01(d / falloff);
                t = t * t * (3f - 2f * t);
                if (t <= 0f) continue;
                heights[zi, xi] = Mathf.Lerp(heights[zi, xi], target, t);
            }
        }
        td.SetHeights(hxBase, hyBase, heights);
    }

    void ClearTreesInFootprint(TerrainData td, Vector3 corner, float fieldWidth, float fieldLength,
        float xMin, float xMax, float zMin, float zMax)
    {
        var instances = td.treeInstances;
        var kept = new List<TreeInstance>(instances.Length);
        int removed = 0;
        foreach (var t in instances)
        {
            float worldX = corner.x + t.position.x * fieldWidth;
            float worldZ = corner.z + t.position.z * fieldLength;
            if (worldX >= xMin && worldX <= xMax && worldZ >= zMin && worldZ <= zMax) { removed++; continue; }
            kept.Add(t);
        }
        if (removed > 0)
        {
            td.SetTreeInstances(kept.ToArray(), true);
            Debug.Log($"[FarmlandPlot] Cleared {removed} tree instance(s) from the plot footprint.");
        }
    }

    void RepaintGrass(TerrainData td, Vector3 corner, float fieldWidth, float fieldLength,
        float xMin, float xMax, float zMin, float zMax)
    {
        int layerCount = td.terrainLayers.Length;
        if (layerCount == 0) return;
        int grass = 0;
        for (int i = 0; i < layerCount; i++)
        {
            var l = td.terrainLayers[i];
            if (l == null) continue;
            string n = l.name.ToLowerInvariant();
            if (n.Contains("grass") && !n.Contains("flower")) { grass = i; break; }
        }

        int res = td.alphamapResolution;
        var (axBase, ayBase, aw, ah) = ClampRegion(corner, fieldWidth, fieldLength, res, xMin, xMax, zMin, zMax);
        if (aw <= 0 || ah <= 0) return;

        var maps = td.GetAlphamaps(axBase, ayBase, aw, ah);
        for (int az = 0; az < ah; az++)
            for (int ax = 0; ax < aw; ax++)
            {
                for (int l = 0; l < layerCount; l++) maps[az, ax, l] = 0f;
                maps[az, ax, grass] = 1f;
            }
        td.SetAlphamaps(axBase, ayBase, maps);
    }

    static (int xBase, int yBase, int w, int h) ClampRegion(Vector3 corner, float fieldWidth, float fieldLength,
        int res, float xMinW, float xMaxW, float zMinW, float zMaxW)
    {
        float uMin = Mathf.Clamp01((xMinW - corner.x) / fieldWidth);
        float uMax = Mathf.Clamp01((xMaxW - corner.x) / fieldWidth);
        float vMin = Mathf.Clamp01((zMinW - corner.z) / fieldLength);
        float vMax = Mathf.Clamp01((zMaxW - corner.z) / fieldLength);
        int xBase = Mathf.FloorToInt(uMin * (res - 1));
        int xEnd = Mathf.Min(res - 1, Mathf.CeilToInt(uMax * (res - 1)));
        int yBase = Mathf.FloorToInt(vMin * (res - 1));
        int yEnd = Mathf.Min(res - 1, Mathf.CeilToInt(vMax * (res - 1)));
        return (xBase, yBase, Mathf.Max(0, xEnd - xBase + 1), Mathf.Max(0, yEnd - yBase + 1));
    }

    // ── Dressing ───────────────────────────────────────────────────────────────────────────────────────
    void BuildFence(Transform root, float xMin, float xMax, float zMin, float zMax)
    {
        var fence = AssetDatabase.LoadAssetAtPath<GameObject>(FenceStyles[Mathf.Clamp(_fenceStyle, 0, FenceStyles.Length - 1)]);
        var gate = AssetDatabase.LoadAssetAtPath<GameObject>(Farm + "SM_Prop_Meadow_Fence_Gate_01.prefab");
        if (fence == null) { Debug.LogWarning("[FarmlandPlot] Meadow fence prefab missing — is Nature Biomes imported?"); return; }

        var probe = (GameObject)PrefabUtility.InstantiatePrefab(fence);
        TerrainShapingUtil.MeasureFootprint(probe, out var fSize, out _);
        Object.DestroyImmediate(probe);
        float span = Mathf.Max(fSize.x, fSize.z);
        if (span < 0.5f) span = 3f;

        var fenceRoot = new GameObject("Fence").transform;
        fenceRoot.SetParent(root, false);

        BuildEdge(fenceRoot, fence, gate, span, new Vector2(xMin, zMin), new Vector2(xMax, zMin), true);
        BuildEdge(fenceRoot, fence, null, span, new Vector2(xMax, zMin), new Vector2(xMax, zMax), false);
        BuildEdge(fenceRoot, fence, null, span, new Vector2(xMax, zMax), new Vector2(xMin, zMax), false);
        BuildEdge(fenceRoot, fence, null, span, new Vector2(xMin, zMax), new Vector2(xMin, zMin), false);
    }

    void BuildEdge(Transform parent, GameObject fencePrefab, GameObject gatePrefab, float span,
        Vector2 from, Vector2 to, bool withGate)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        Vector2 dir = delta / Mathf.Max(length, 0.001f);
        float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        int count = Mathf.Max(1, Mathf.RoundToInt(length / span));
        int gateIndex = withGate && gatePrefab != null ? count / 2 : -1;

        for (int i = 0; i < count; i++)
        {
            float t = (i + 0.5f) / count;
            Vector2 pos2 = from + delta * t;
            var prefab = i == gateIndex ? gatePrefab : fencePrefab;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            float y = TerrainShapingUtil.SurfaceY(_terrain, _terrain.transform.position, _terrain.terrainData.size.x, _terrain.terrainData.size.z, pos2.x, pos2.y);
            go.transform.position = new Vector3(pos2.x, y, pos2.y);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    void BuildCrops(Transform root, float xMin, float xMax, float zMin, float zMax, System.Random rng)
    {
        var clumps = new List<GameObject>();
        foreach (var p in CropClumps)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go != null) clumps.Add(go);
        }
        if (clumps.Count == 0) { Debug.LogWarning("[FarmlandPlot] No crop clump prefabs found under " + FarmRoot); return; }

        var cropRoot = new GameObject("Crops").transform;
        cropRoot.SetParent(root, false);

        float ix0 = xMin + _cropInset, ix1 = xMax - _cropInset;
        float iz0 = zMin + _cropInset, iz1 = zMax - _cropInset;
        if (ix1 <= ix0 || iz1 <= iz0) { Debug.LogWarning("[FarmlandPlot] Plot too small for the crop inset — widen the plot or shrink the inset."); return; }

        for (float wz = iz0; wz <= iz1; wz += _cropSpacing)
            for (float wx = ix0; wx <= ix1; wx += _cropSpacing)
            {
                float jx = wx + (float)(rng.NextDouble() * 2 - 1) * _cropJitter;
                float jz = wz + (float)(rng.NextDouble() * 2 - 1) * _cropJitter;
                var prefab = clumps[rng.Next(clumps.Count)];
                PlaceOnSurface(cropRoot, prefab, jx, jz, rng, 0.15f);
            }
    }

    void PlaceOnSurface(Transform parent, GameObject prefab, float worldX, float worldZ, System.Random rng, float scaleVar)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        float y = TerrainShapingUtil.SurfaceY(_terrain, _terrain.transform.position, _terrain.terrainData.size.x, _terrain.terrainData.size.z, worldX, worldZ);
        go.transform.position = new Vector3(worldX, y, worldZ);
        go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
        if (scaleVar > 0f)
            go.transform.localScale *= 1f + ((float)rng.NextDouble() * 2f - 1f) * scaleVar;
    }
}
