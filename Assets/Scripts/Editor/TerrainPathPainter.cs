using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 3.1.9 — paints a trail directly into the terrain's splatmap (a Synty footpath/dirt/gravel layer) along a
/// polyline of waypoint empties. Replaces the old <see cref="TrailBuilder"/> that laid dirt PROP tiles — this
/// version has no GameObjects, no z-fighting, no colliders, carves nothing from the navmesh, and conforms to
/// the terrain perfectly (it IS the terrain surface). Requires the terrain to be textured first
/// (<c>Tools/Zones/Apply Synty Terrain Textures</c>) so the footpath/dirt layers exist to paint.
///
/// Workflow (same waypoint convention as the old tool, so existing "TrailRoute" objects just work):
///   1. Create an empty GameObject (e.g. "TrailRoute") with child empties where the path bends (in order).
///   2. Tools/Zones/Paint Path Along Children → set the route + layer + width → Paint.
/// Only the XZ of each waypoint is used; the path drapes over hills via the splatmap. Re-runnable — re-paint
/// after nudging markers (use "Reset base splat" to wipe the old path first, or paint a wider grass pass over it).
///
/// Menu: <c>Tools/Zones/Paint Path Along Children</c>.
/// </summary>
public class TerrainPathPainter : EditorWindow
{
    Terrain _terrain;
    GameObject _route;
    int _layerIndex;
    float _width = 6f;      // full-strength path width (world units)
    float _falloff = 2.5f;  // soft edge band beyond the full-width half
    string[] _layerNames = new string[0];

    [MenuItem("Tools/Zones/Paint Path Along Children")]
    static void Open()
    {
        var w = GetWindow<TerrainPathPainter>("Paint Path");
        w.minSize = new Vector2(320, 240);
        w.Refresh();
    }

    void OnFocus() => Refresh();

    void Refresh()
    {
        var go = GameObject.Find("ZoneTerrain");
        _terrain = go != null ? go.GetComponent<Terrain>() : FindFirstObjectByType<Terrain>();

        var names = new List<string>();
        if (_terrain != null)
        {
            foreach (var l in _terrain.terrainData.terrainLayers)
                names.Add(l != null ? l.name : "(none)");
        }
        _layerNames = names.ToArray();

        // Default to the footpath layer if present.
        if (_layerNames.Length > 0)
        {
            _layerIndex = Mathf.Clamp(_layerIndex, 0, _layerNames.Length - 1);
            for (int i = 0; i < _layerNames.Length; i++)
                if (_layerNames[i].ToLower().Contains("footpath")) { _layerIndex = i; break; }
        }

        if (_route == null && Selection.activeGameObject != null)
            _route = Selection.activeGameObject;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Terrain", EditorStyles.boldLabel);
        _terrain = (Terrain)EditorGUILayout.ObjectField("Target terrain", _terrain, typeof(Terrain), true);
        if (_terrain == null)
        {
            EditorGUILayout.HelpBox("No terrain. Run Tools/Zones/Build Terrain Zone, then Apply Synty Terrain Textures.", MessageType.Warning);
            return;
        }
        if (_layerNames.Length == 0)
        {
            EditorGUILayout.HelpBox("This terrain has no terrain layers. Run Tools/Zones/Apply Synty Terrain Textures first.", MessageType.Warning);
            if (GUILayout.Button("Refresh")) Refresh();
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Path", EditorStyles.boldLabel);
        _route = (GameObject)EditorGUILayout.ObjectField("Route (waypoint parent)", _route, typeof(GameObject), true);
        _layerIndex = EditorGUILayout.Popup("Paint layer", _layerIndex, _layerNames);
        _width   = EditorGUILayout.Slider("Width (u)", _width, 1f, 20f);
        _falloff = EditorGUILayout.Slider("Soft edge (u)", _falloff, 0f, 8f);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(_route == null))
            if (GUILayout.Button("Paint Path", GUILayout.Height(30)))
                PaintPath();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Maintenance", EditorStyles.miniBoldLabel);
        if (GUILayout.Button("Reset base splat (grass / rock / dirt) — wipes hand-painting"))
        {
            if (EditorUtility.DisplayDialog("Reset terrain splat?",
                "Repaint the automatic grass/rock/dirt splat by height + slope. This erases ALL hand-painted layers " +
                "(paths, flowers, mud). Continue?", "Reset", "Cancel"))
            {
                TerrainTextureSetup.RegenerateSplat(_terrain);
                MarkDirty();
                Debug.Log("[PathPainter] Reset terrain splat to the height/slope base.");
            }
        }
    }

    void PaintPath()
    {
        var waypoints = new List<Vector3>();
        foreach (Transform c in _route.transform)
            waypoints.Add(c.position);
        if (waypoints.Count < 2)
        {
            Debug.LogWarning($"[PathPainter] '{_route.name}' needs at least 2 child waypoint empties (found {waypoints.Count}).");
            return;
        }

        var td = _terrain.terrainData;
        int w = td.alphamapWidth, h = td.alphamapHeight, layers = td.alphamapLayers;
        var maps = td.GetAlphamaps(0, 0, w, h);
        Vector3 tpos = _terrain.transform.position;
        float sx = td.size.x, sz = td.size.z;
        float half = _width * 0.5f;
        float outer = half + _falloff;
        int painted = 0;

        // Alphamap is indexed [z, x, layer]; x maps to terrain X, z to terrain Z.
        for (int az = 0; az < h; az++)
        {
            float worldZ = tpos.z + (az / (float)(h - 1)) * sz;
            for (int ax = 0; ax < w; ax++)
            {
                float worldX = tpos.x + (ax / (float)(w - 1)) * sx;
                float d = DistanceToPolyline(worldX, worldZ, waypoints);
                if (d > outer) continue;

                float t = d <= half ? 1f : 1f - (d - half) / Mathf.Max(_falloff, 1e-4f);
                t = t * t * (3f - 2f * t); // smoothstep edge

                float keep = 1f - t;
                for (int l = 0; l < layers; l++) maps[az, ax, l] *= keep;
                maps[az, ax, _layerIndex] += t;
                painted++;
            }
        }

        td.SetAlphamaps(0, 0, maps);
        MarkDirty();
        Debug.Log($"[PathPainter] Painted '{_layerNames[_layerIndex]}' along {waypoints.Count} waypoint(s) " +
                  $"({painted} splat texel(s), width {_width}u + {_falloff}u edge) on '{_terrain.name}'.");
    }

    // Min distance (XZ) from a point to a polyline of waypoints.
    static float DistanceToPolyline(float px, float pz, List<Vector3> pts)
    {
        float best = float.MaxValue;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            float d = DistToSegment(px, pz, pts[i].x, pts[i].z, pts[i + 1].x, pts[i + 1].z);
            if (d < best) best = d;
        }
        return best;
    }

    static float DistToSegment(float px, float pz, float ax, float az, float bx, float bz)
    {
        float dx = bx - ax, dz = bz - az;
        float len2 = dx * dx + dz * dz;
        float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / len2);
        float cx = ax + t * dx, cz = az + t * dz;
        float ex = px - cx, ez = pz - cz;
        return Mathf.Sqrt(ex * ex + ez * ez);
    }

    void MarkDirty()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.SetDirty(_terrain.terrainData);
    }
}
