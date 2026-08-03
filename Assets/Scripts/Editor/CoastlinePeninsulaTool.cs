using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Carves a headland/peninsula into the west coastline of the EXISTING, already-decorated ZoneTerrain —
/// entirely in place, via partial GetHeights/SetHeights + GetAlphamaps/SetAlphamaps on the live TerrainData.
/// Never recreates the TerrainData asset the way <c>Tools/Zones/Build Terrain Zone</c> does, which silently
/// wipes any hand-painted trees (see the 2026-07-09 CLAUDE.md note) — this tool only touches the affected
/// footprint, so it's safe to run on a fully-dressed field.
///
/// Technique: for a band of coastline centered on "Position along coast," samples the terrain's OWN existing
/// height a bit further inland (the "anchor") and blends that height westward into the sea band, tapering by
/// distance from the band center (north/south) and by distance from the anchor (east/west). The point is
/// built from the real adjacent hill shape, not a synthetic new one. A tighter secondary pass flattens a
/// small pad at the tip for a lighthouse foundation. A second section places an assembled "lighthouse" group
/// (tower prefab + beacon light + optional light-ray FX) at the carved tip.
///
/// No dedicated lighthouse mesh exists in any owned Synty pack — the tower prefab field defaults to the
/// Dungeon pack's Goblin Tower as a stand-in stone-tower silhouette and is swappable; re-placing is cheap
/// (one "Lighthouse" group, delete and redo).
///
/// Heightmap edits have no reliable in-editor Undo — commit
/// <c>Assets/Scenes/SampleScene/Terrain/CreslinsTerrainData.asset</c> to git before running this.
///
/// Menu: <c>Tools/Zones/Carve Coastline Peninsula</c>.
/// </summary>
public class CoastlinePeninsulaTool : EditorWindow
{
    const string DungeonProps = "Assets/Synty/PolygonDungeon/Prefabs/Props/";
    const string BeamFxPath = "Assets/Synty/PolygonGeneric/Prefabs/FX/LightRay_Round_01.prefab";

    Terrain _terrain;

    float _coastPosition01 = 0.33f; // 0 = south edge, 1 = north edge
    float _bandHalfWidth = 90f;     // north-south half-extent of the point
    float _falloff = 60f;           // extra soft taper beyond the half-width
    float _anchorU = 0.16f;         // sample point inland (fraction of field width) used as the "already land" reference
    float _reach01 = 0.75f;         // how far toward the sea (as a fraction of the anchor's distance from the west edge) the point pushes
    float _tipFlattenRadius = 16f;

    GameObject _towerPrefab;
    float _towerScale = 1f;
    bool _addPointLight = true;
    Color _lightColor = new(1f, 0.72f, 0.35f);
    float _lightRange = 45f;
    float _lightIntensity = 3f;
    bool _addBeamFx = true;

    Vector3 _lastTip;
    bool _haveTip;

    [MenuItem("Tools/Zones/Carve Coastline Peninsula")]
    static void Open()
    {
        var w = GetWindow<CoastlinePeninsulaTool>("Coastline Peninsula");
        w.minSize = new Vector2(360, 480);
        w.Refresh();
    }

    void OnFocus() => Refresh();

    void Refresh()
    {
        var go = GameObject.Find("ZoneTerrain");
        _terrain = go != null ? go.GetComponent<Terrain>() : FindFirstObjectByType<Terrain>();
        if (_towerPrefab == null)
            _towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DungeonProps + "SM_Prop_Goblin_Tower_01.prefab");
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Edits the LIVE terrain in place (safe for painted trees) — never regenerates via Build Terrain " +
            "Zone. Commit CreslinsTerrainData.asset to git first; there's no in-editor undo for heightmap edits.",
            MessageType.Warning);

        _terrain = (Terrain)EditorGUILayout.ObjectField("Target terrain", _terrain, typeof(Terrain), true);
        if (_terrain == null)
        {
            EditorGUILayout.HelpBox("No terrain found. Run Tools/Zones/Build Terrain Zone first.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Peninsula", EditorStyles.boldLabel);
        _coastPosition01 = EditorGUILayout.Slider("Position along coast (0=south, 1=north)", _coastPosition01, 0f, 1f);
        _bandHalfWidth = EditorGUILayout.Slider("Half-width (u, north-south)", _bandHalfWidth, 20f, 300f);
        _falloff = EditorGUILayout.Slider("Soft taper (u)", _falloff, 10f, 200f);
        _anchorU = EditorGUILayout.Slider("Anchor position inland (0-1)", _anchorU, 0.08f, 0.35f);
        _reach01 = EditorGUILayout.Slider("Reach toward the sea (0-1)", _reach01, 0.05f, 0.95f);
        _tipFlattenRadius = EditorGUILayout.Slider("Tip flatten radius (u)", _tipFlattenRadius, 4f, 60f);

        EditorGUILayout.Space();
        if (GUILayout.Button("Carve Peninsula", GUILayout.Height(30)))
            Carve();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lighthouse", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "No dedicated lighthouse mesh exists in any owned pack. Default reuses the Dungeon pack's Goblin " +
            "Tower as a stone-tower silhouette — swap the prefab field below and re-place if it doesn't read " +
            "right; the result is one 'Lighthouse' group, cheap to delete and redo.",
            MessageType.None);
        _towerPrefab = (GameObject)EditorGUILayout.ObjectField("Tower prefab", _towerPrefab, typeof(GameObject), false);
        _towerScale = EditorGUILayout.Slider("Tower scale", _towerScale, 0.5f, 4f);
        _addPointLight = EditorGUILayout.Toggle("Add beacon point light", _addPointLight);
        if (_addPointLight)
        {
            _lightColor = EditorGUILayout.ColorField("Light color", _lightColor);
            _lightRange = EditorGUILayout.Slider("Light range", _lightRange, 10f, 150f);
            _lightIntensity = EditorGUILayout.Slider("Light intensity", _lightIntensity, 0.5f, 10f);
        }
        _addBeamFx = EditorGUILayout.Toggle("Add light-ray FX", _addBeamFx);

        using (new EditorGUI.DisabledScope(_towerPrefab == null))
        {
            string label = _haveTip ? "Place Lighthouse At Last Carved Tip" : "Place Lighthouse At Scene View Pivot";
            if (GUILayout.Button(label, GUILayout.Height(28)))
                PlaceLighthouse();
        }
    }

    void Carve()
    {
        var td = _terrain.terrainData;
        Vector3 corner = _terrain.transform.position;
        float fieldWidth = td.size.x, fieldLength = td.size.z;

        float centerZ = corner.z + _coastPosition01 * fieldLength;
        float zMin = centerZ - _bandHalfWidth - _falloff;
        float zMax = centerZ + _bandHalfWidth + _falloff;

        float anchorX = corner.x + _anchorU * fieldWidth;
        float reachWorld = _anchorU * fieldWidth * _reach01;
        float xMin = corner.x;
        float xMax = anchorX;

        var (hxBase, hyBase, hw, hh) = ClampRegion(corner, fieldWidth, fieldLength, td.heightmapResolution, xMin, xMax, zMin, zMax);
        if (hw <= 0 || hh <= 0) { Debug.LogWarning("[CoastlinePeninsula] Region out of bounds."); return; }

        var heights = td.GetHeights(hxBase, hyBase, hw, hh);
        int res = td.heightmapResolution;

        float tipU = float.MaxValue;
        Vector3 tipWorld = new(anchorX, 0f, centerZ);

        for (int zi = 0; zi < hh; zi++)
        {
            int gz = hyBase + zi;
            float v = gz / (float)(res - 1);
            float worldZ = corner.z + v * fieldLength;
            float zWeight = BandWeight(worldZ, centerZ, _bandHalfWidth, _falloff);
            if (zWeight <= 0f) continue;

            float anchorHeight01 = td.GetInterpolatedHeight(_anchorU, v) / td.size.y;

            for (int xi = 0; xi < hw; xi++)
            {
                int gx = hxBase + xi;
                float u = gx / (float)(res - 1);
                float worldX = corner.x + u * fieldWidth;

                float distFromAnchor = anchorX - worldX; // positive = west of anchor
                float pull = 1f - Mathf.Clamp01(distFromAnchor / Mathf.Max(reachWorld, 1f));
                pull = pull * pull * (3f - 2f * pull);
                float blend = pull * zWeight;
                if (blend <= 0f) continue;

                heights[zi, xi] = Mathf.Lerp(heights[zi, xi], anchorHeight01, blend);

                if (blend > 0.5f && u < tipU) { tipU = u; tipWorld = new Vector3(worldX, 0f, worldZ); }
            }
        }
        td.SetHeights(hxBase, hyBase, heights);

        if (tipU < float.MaxValue)
            FlattenTip(td, corner, fieldWidth, fieldLength, tipWorld, _tipFlattenRadius);

        RepaintSplat(td, corner, fieldWidth, fieldLength, xMin - 10f, xMax + 10f, zMin, zMax);
        _terrain.Flush();

        _lastTip = new Vector3(tipWorld.x, TerrainShapingUtil.SurfaceY(_terrain, corner, fieldWidth, fieldLength, tipWorld.x, tipWorld.z), tipWorld.z);
        _haveTip = true;

        EditorUtility.SetDirty(td);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[CoastlinePeninsula] Carved a headland at coast position {_coastPosition01:F2} (tip ~{_lastTip}). " +
                  "NEXT: rebake the navmesh (Tools/Terrain/Rebake NavMesh); eyeball the west boundary rocks/water " +
                  "plane near the point (they're separate GameObjects, untouched by this — nudge or remove any " +
                  "that now clip the new land); then Place Lighthouse.");
    }

    void FlattenTip(TerrainData td, Vector3 corner, float fieldWidth, float fieldLength, Vector3 tip, float radius)
    {
        float pad = 10f;
        var (hxBase, hyBase, hw, hh) = ClampRegion(corner, fieldWidth, fieldLength, td.heightmapResolution,
            tip.x - radius - pad, tip.x + radius + pad, tip.z - radius - pad, tip.z + radius + pad);
        if (hw <= 0 || hh <= 0) return;

        var heights = td.GetHeights(hxBase, hyBase, hw, hh);
        int res = td.heightmapResolution;
        float padHeight01 = td.GetInterpolatedHeight(
            Mathf.Clamp01((tip.x - corner.x) / fieldWidth), Mathf.Clamp01((tip.z - corner.z) / fieldLength)) / td.size.y;

        for (int zi = 0; zi < hh; zi++)
        {
            int gz = hyBase + zi;
            float worldZ = corner.z + gz / (float)(res - 1) * fieldLength;
            for (int xi = 0; xi < hw; xi++)
            {
                int gx = hxBase + xi;
                float worldX = corner.x + gx / (float)(res - 1) * fieldWidth;
                float d = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(tip.x, tip.z));
                float t = 1f - Mathf.Clamp01((d - radius) / 10f);
                t = t * t * (3f - 2f * t);
                if (t <= 0f) continue;
                heights[zi, xi] = Mathf.Lerp(heights[zi, xi], padHeight01, t);
            }
        }
        td.SetHeights(hxBase, hyBase, heights);
    }

    void PlaceLighthouse()
    {
        if (_towerPrefab == null) return;

        Vector3 pos;
        if (_haveTip) pos = _lastTip;
        else
        {
            Vector3 pivot = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            pos = new Vector3(pivot.x,
                TerrainShapingUtil.SurfaceY(_terrain, _terrain.transform.position, _terrain.terrainData.size.x, _terrain.terrainData.size.z, pivot.x, pivot.z),
                pivot.z);
        }

        var root = new GameObject("Lighthouse").transform;
        root.position = pos;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Place Lighthouse");

        var tower = (GameObject)PrefabUtility.InstantiatePrefab(_towerPrefab, root);
        tower.transform.localPosition = Vector3.zero;
        tower.transform.localScale = Vector3.one * _towerScale;

        float towerHeight = MeasureHeight(tower);

        if (_addPointLight)
        {
            var lightGo = new GameObject("BeaconLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, towerHeight * 0.92f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = _lightColor;
            light.range = _lightRange;
            light.intensity = _lightIntensity;

            if (_addBeamFx)
            {
                var beamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BeamFxPath);
                if (beamPrefab != null)
                {
                    var beam = (GameObject)PrefabUtility.InstantiatePrefab(beamPrefab, root);
                    beam.transform.localPosition = new Vector3(0f, towerHeight * 0.9f, 0f);
                }
                else
                {
                    Debug.LogWarning("[CoastlinePeninsula] Light-ray FX prefab not found at " + BeamFxPath);
                }
            }
        }

        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[CoastlinePeninsula] Placed lighthouse at {pos}. Eyeball scale/height and swap the tower " +
                  "prefab if the Goblin Tower doesn't read right — it's all one 'Lighthouse' group, easy to " +
                  "delete and redo.");
    }

    // ── Local math helpers ────────────────────────────────────────────────────────────────────────────
    static float BandWeight(float worldZ, float centerZ, float halfWidth, float falloff)
    {
        float d = Mathf.Abs(worldZ - centerZ);
        if (d <= halfWidth) return 1f;
        float t = 1f - Mathf.Clamp01((d - halfWidth) / Mathf.Max(falloff, 1e-3f));
        return t * t * (3f - 2f * t);
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

    static void RepaintSplat(TerrainData td, Vector3 corner, float fieldWidth, float fieldLength,
        float xMinW, float xMaxW, float zMinW, float zMaxW)
    {
        int layerCount = td.terrainLayers.Length;
        if (layerCount == 0) return;
        int grass = FindLayer(td, "grass", "flower");
        int dirt = FindLayer(td, "dirt");
        int rock = FindLayer(td, "rock");
        if (grass < 0) grass = 0;

        int res = td.alphamapResolution;
        var (axBase, ayBase, aw, ah) = ClampRegion(corner, fieldWidth, fieldLength, res, xMinW, xMaxW, zMinW, zMaxW);
        if (aw <= 0 || ah <= 0) return;

        var maps = td.GetAlphamaps(axBase, ayBase, aw, ah);
        float ty = td.size.y;

        for (int az = 0; az < ah; az++)
        {
            int gz = ayBase + az;
            float v = gz / (float)(res - 1);
            for (int ax = 0; ax < aw; ax++)
            {
                int gx = axBase + ax;
                float u = gx / (float)(res - 1);
                float steep = td.GetSteepness(u, v);
                float hh = td.GetInterpolatedHeight(u, v) / ty;

                float wRock = Mathf.Clamp01((steep - 15f) / 22f);
                wRock = Mathf.Max(wRock, Mathf.Clamp01((hh - 0.44f) / 0.22f));
                float wDirt = Mathf.Clamp01((0.17f - hh) / 0.10f);
                float wGrass = Mathf.Max(0f, 1f - wRock - wDirt);
                float s = wGrass + wRock + wDirt;
                if (s < 1e-4f) { wGrass = 1f; s = 1f; }

                for (int l = 0; l < layerCount; l++) maps[az, ax, l] = 0f;
                maps[az, ax, grass] = wGrass / s;
                if (dirt >= 0) maps[az, ax, dirt] = wDirt / s;
                if (rock >= 0) maps[az, ax, rock] = wRock / s;
            }
        }
        td.SetAlphamaps(axBase, ayBase, maps);
    }

    // Finds a layer whose name contains `mustContain` but not `mustExclude` (e.g. grass-but-not-flowers).
    static int FindLayer(TerrainData td, string mustContain, string mustExclude = null)
    {
        for (int i = 0; i < td.terrainLayers.Length; i++)
        {
            var l = td.terrainLayers[i];
            if (l == null) continue;
            string n = l.name.ToLowerInvariant();
            if (n.Contains(mustContain) && (mustExclude == null || !n.Contains(mustExclude))) return i;
        }
        return -1;
    }

    static float MeasureHeight(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 3f;
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b.size.y;
    }
}
