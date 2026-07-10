using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 3.1.9 / 3.6 helper — sprinkle vegetation across the zone at a density you set. Pick categories + a
/// saturation, choose the whole field or a circle around the selected object, and hit Scatter. Props
/// conform to the ground (raycast), keep a clear radius around spawn, and accumulate under a "Scattered" root
/// so you can layer passes (e.g. sparse trees, then denser undergrowth). Clear wipes it. Menu:
/// <c>Tools/Zones/Scatter Props</c>.
/// </summary>
public class ScatterWindow : EditorWindow
{
    const string AdvEnv = "Assets/Synty/PolygonAdventure/Prefabs/Environments/";
    const string Root   = "Scattered";

    [System.Flags]
    enum Cat { Trees = 1, Pines = 2, Rocks = 4, DeadTrees = 8, Bushes = 16, Flowers = 32, Reeds = 64 }

    Cat   _cats        = Cat.Trees | Cat.Pines;
    float _density     = 0.3f;  // props per 100 u²
    bool  _wholeField  = true;
    float _radius      = 120f;  // when scattering around the selection
    float _spawnClear  = 45f;
    float _scaleVar    = 0.25f;

    [MenuItem("Tools/Zones/Scatter Props")]
    static void Open() => GetWindow<ScatterWindow>("Scatter Props");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel);
        _cats = (Cat)EditorGUILayout.EnumFlagsField(_cats);

        EditorGUILayout.Space();
        _density = EditorGUILayout.Slider(new GUIContent("Density (per 100 u²)"), _density, 0.02f, 2f);
        _scaleVar = EditorGUILayout.Slider("Scale variation", _scaleVar, 0f, 0.6f);
        _spawnClear = EditorGUILayout.FloatField("Clear radius around spawn", _spawnClear);

        EditorGUILayout.Space();
        _wholeField = EditorGUILayout.Toggle("Whole field (else around selection)", _wholeField);
        if (!_wholeField) _radius = EditorGUILayout.FloatField("Radius around selection", _radius);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scatter", GUILayout.Height(28))) Scatter();
            if (GUILayout.Button("Clear All Scattered", GUILayout.Height(28))) ClearAll();
        }
        EditorGUILayout.HelpBox("Layer passes for a natural look (sparse trees, then bushes). Props on the road/POIs " +
            "aren't auto-avoided — scatter first, then place roads/POIs, or delete the few overlaps by hand.", MessageType.Info);
    }

    void Scatter()
    {
        var pool = BuildPool();
        if (pool.Count == 0) { Debug.LogWarning("[Scatter] No prefabs for the selected categories."); return; }

        // Area (XZ rect for the field, or a circle around the selection).
        Vector3 center; float halfX, halfZ; bool circle = !_wholeField;
        if (_wholeField)
        {
            var ground = GameObject.Find("Ground");
            if (ground == null) { Debug.LogWarning("[Scatter] No 'Ground' found for whole-field scatter."); return; }
            center = ground.transform.position;
            halfX = ground.transform.localScale.x * 5f; // plane = 10u @ scale 1
            halfZ = ground.transform.localScale.z * 5f;
        }
        else
        {
            var sel = Selection.activeTransform;
            if (sel == null) { Debug.LogWarning("[Scatter] Select an object to scatter around."); return; }
            center = sel.position; halfX = halfZ = _radius;
        }

        float area = circle ? Mathf.PI * _radius * _radius : (halfX * 2f) * (halfZ * 2f);
        int count = Mathf.RoundToInt(_density * area / 100f);
        if (count <= 0) return;

        Vector3 spawn = SpawnPos();
        var parent = Ensure(Root);
        Physics.SyncTransforms();
        var rng = new System.Random(System.Environment.TickCount);
        int placed = 0, guard = count * 12;

        while (placed < count && guard-- > 0)
        {
            float x = center.x + (float)(rng.NextDouble() * 2 - 1) * halfX;
            float z = center.z + (float)(rng.NextDouble() * 2 - 1) * halfZ;
            if (circle && (new Vector2(x - center.x, z - center.z)).sqrMagnitude > _radius * _radius) continue;
            if ((new Vector2(x - spawn.x, z - spawn.z)).sqrMagnitude < _spawnClear * _spawnClear) continue;

            if (!Ground(new Vector3(x, 0, z), out var surface)) continue;

            var (prefab, keepCollider) = pool[rng.Next(pool.Count)];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = surface;
            go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
            go.transform.localScale *= 1f + ((float)(rng.NextDouble() * 2 - 1)) * _scaleVar;
            if (!keepCollider) StripColliders(go);
            placed++;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[Scatter] Placed {placed} prop(s).");
    }

    void ClearAll()
    {
        var go = GameObject.Find(Root);
        if (go != null) Object.DestroyImmediate(go);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // (prefab, keepCollider) — solids (trees/rocks) block + carve navmesh; foliage is stripped + would be
    // nav-excluded in a dedicated pass (kept simple here — foliage has no collider so it never blocks/paths).
    List<(GameObject, bool)> BuildPool()
    {
        var pool = new List<(GameObject, bool)>();
        void Add(bool keep, params string[] names) { foreach (var n in names) { var p = AssetDatabase.LoadAssetAtPath<GameObject>(AdvEnv + n + ".prefab"); if (p != null) pool.Add((p, keep)); } }

        if (_cats.HasFlag(Cat.Trees))     Add(true,  "SM_Env_Tree_01", "SM_Env_Tree_02", "SM_Env_Tree_03", "SM_Env_Tree_04", "SM_Env_Tree_05", "SM_Env_Tree_06");
        if (_cats.HasFlag(Cat.Pines))     Add(true,  "SM_Env_TreePine_01", "SM_Env_TreePine_02", "SM_Env_TreePine_03");
        if (_cats.HasFlag(Cat.Rocks))     Add(true,  "SM_Env_Rock_01", "SM_Env_Rock_02", "SM_Env_Rock_03", "SM_Env_Rock_04", "SM_Env_Rock_05");
        if (_cats.HasFlag(Cat.DeadTrees)) Add(true,  "SM_Env_TreeDead_01", "SM_Env_TreeDead_02");
        if (_cats.HasFlag(Cat.Bushes))    Add(false, "SM_Env_Bush_01", "SM_Env_Bush_02", "SM_Env_Bush_03", "SM_Env_Bush_04");
        if (_cats.HasFlag(Cat.Flowers))   Add(false, "SM_Env_Flower_01", "SM_Env_Flower_02", "SM_Env_Flower_03", "SM_Env_Flower_04", "SM_Env_Flower_05", "SM_Env_Flower_06");
        if (_cats.HasFlag(Cat.Reeds))     Add(false, "SM_Env_Reeds_01", "SM_Env_Reeds_02", "SM_Env_Reeds_03");
        return pool;
    }

    static readonly RaycastHit[] _hits = new RaycastHit[16];
    static bool Ground(Vector3 xz, out Vector3 point)
    {
        point = default;
        int n = Physics.RaycastNonAlloc(new Vector3(xz.x, 500f, xz.z), Vector3.down, _hits, 1000f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; bool found = false;
        for (int i = 0; i < n; i++)
        {
            var t = _hits[i].collider.transform;
            bool skip = false;
            for (var x = t; x != null; x = x.parent) if (x.name == Root || x.name == "TrellisHub" || x.name == "PathTiles") { skip = true; break; }
            if (skip) continue;
            if (_hits[i].distance < best) { best = _hits[i].distance; point = _hits[i].point; found = true; }
        }
        return found;
    }

    static Vector3 SpawnPos()
    {
        var s = Object.FindFirstObjectByType<Mirror.NetworkStartPosition>();
        return s != null ? s.transform.position : new Vector3(0, 0, -5);
    }

    static Transform Ensure(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) go = new GameObject(name);
        return go.transform;
    }

    static void StripColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }
}
