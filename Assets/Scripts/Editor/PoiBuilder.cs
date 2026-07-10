using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 3.1.9 / 3.6 — curated point-of-interest builders (like the Trellis hub) so the big field has landmarks.
/// Select an empty where you want it (or nothing → a default spot north of spawn), run a builder; it drops a
/// re-runnable cluster there, conformed to the ground. Menu: <c>Tools/Zones/POI/…</c>.
/// </summary>
public static class PoiBuilder
{
    const string Base     = "Assets/Synty/PolygonGeneric/Prefabs/Base/";
    const string GenProps = "Assets/Synty/PolygonGeneric/Prefabs/Props/";
    const string GenChr   = "Assets/Synty/PolygonGeneric/Prefabs/Characters/";
    const string GenEnv    = "Assets/Synty/PolygonGeneric/Prefabs/Environment/";
    const string RivalProp = "Assets/Synty/PolygonFantasyRivals/Prefabs/Props/";
    const string AdvEnv    = "Assets/Synty/PolygonAdventure/Prefabs/Environments/";

    // (folder, name, offsetX, offsetZ, yaw, keepCollider)
    struct P { public string f, n; public float x, z, yaw; public bool col; }
    static P Pc(string f, string n, float x, float z, float yaw, bool col) => new() { f = f, n = n, x = x, z = z, yaw = yaw, col = col };

    // ── Undead ruins ─────────────────────────────────────────────────────────────
    [MenuItem("Tools/Zones/POI/Build Undead Ruins")]
    public static void BuildRuins()
    {
        var root = FreshRoot("UndeadRuins", out Vector3 o, out float baseY);
        var layout = new[]
        {
            // broken walls (keep colliders — they block + carve navmesh)
            Pc(Base, "SM_Bld_Base_Wall_01",           -6, 4,   0, true),
            Pc(Base, "SM_Bld_Base_Wall_Destroyed_01",  -6, 0,   0, true),
            Pc(Base, "SM_Bld_Base_Wall_Half_01",        -6,-4,   0, true),
            Pc(Base, "SM_Bld_Base_Wall_Destroyed_02",    6, 3, 180, true),
            Pc(Base, "SM_Bld_Base_Wall_Half_01",         6,-3, 180, true),
            Pc(Base, "SM_Bld_Base_Wall_01",              2, 7,  90, true),
            Pc(Base, "SM_Bld_Base_Wall_Destroyed_01",   -2,-7,  90, true),
            // broken columns
            Pc(Base, "SM_Bld_Base_Pillar_Half_01",      -3, 2,  20, true),
            Pc(Base, "SM_Bld_Base_Pillar_Half_02",       3,-2, 200, true),
            Pc(Base, "SM_Bld_Base_Pillar_Half_03",       0, 5, 110, true),
            // centrepiece statue
            Pc(GenProps, "SM_Gen_Prop_Statue_01",        0, 0,   0, true),
            // undead flavour (decorative — no colliders)
            Pc(RivalProp, "SM_Prop_Bones_01",           -2,-1,  40, false),
            Pc(GenProps,  "SM_Gen_Prop_Skull_01",        2, 1, 300, false),
            Pc(GenChr,    "SM_Gen_Chr_Skeleton_01",      1,-3, 150, false),
            // dead trees ringing it
            Pc(AdvEnv, "SM_Env_TreeDead_01",  -9, 8,  30, true),
            Pc(AdvEnv, "SM_Env_TreeDead_02",   9, 7, 210, true),
            Pc(AdvEnv, "SM_Env_TreeDead_01",  -8,-8, 120, true),
            Pc(AdvEnv, "SM_Env_TreeDead_02",  10,-2, 300, true),
        };
        PlaceAll(root, o, baseY, layout);
        Done(root, "Undead ruins");
    }

    // ── Lake ─────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Zones/POI/Build Lake")]
    public static void BuildLake()
    {
        var root = FreshRoot("Lake", out Vector3 o, out float baseY);

        // Water plane scaled to a ~24u pond, just above the grass; no collider (decorative).
        var water = Load(AdvEnv, "SM_Env_Water_01");
        if (water != null)
        {
            var w = (GameObject)PrefabUtility.InstantiatePrefab(water, root);
            MeasureFootprint(w, out var size, out _);
            float diameter = 24f;
            float s = size.x > 0.01f ? diameter / size.x : 2f;
            w.transform.localScale *= s;
            w.transform.position = new Vector3(o.x, baseY + 0.08f, o.z);
            StripColliders(w);
        }

        // Reeds + lilypads + rocks + dead trees ring the shore; river-dirt tiles suggest a muddy bank.
        var ring = new System.Collections.Generic.List<P>();
        void Ring(string f, string n, float r, int count, float yaw0, bool col)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count) * Mathf.PI * 2f + yaw0 * Mathf.Deg2Rad;
                ring.Add(Pc(f, n, Mathf.Cos(a) * r, Mathf.Sin(a) * r, a * Mathf.Rad2Deg + 90f, col));
            }
        }
        Ring(GenEnv, "SM_Gen_Env_Ground_River_Dirt_01", 13f, 10, 0f, false);   // muddy bank (decorative)
        Ring(AdvEnv, "SM_Env_Reeds_01", 12f, 8, 22f, false);
        Ring(AdvEnv, "SM_Env_Reeds_02", 12.5f, 6, 60f, false);
        Ring(GenEnv, "SM_Gen_Env_Lilypads_01", 6f, 4, 15f, false);              // on the water
        Ring(GenEnv, "SM_Gen_Env_Lilypads_02", 8f, 3, 80f, false);
        Ring(AdvEnv, "SM_Env_Rock_02", 15f, 5, 40f, true);
        Ring(AdvEnv, "SM_Env_TreeDead_01", 17f, 3, 90f, true);
        PlaceAll(root, o, baseY, ring.ToArray());

        Done(root, "Lake");
    }

    // ── Shared ───────────────────────────────────────────────────────────────────
    static void PlaceAll(Transform root, Vector3 o, float baseY, P[] layout)
    {
        foreach (var p in layout)
        {
            var prefab = Load(p.f, p.n);
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            float y = p.n.Contains("Lilypad") ? baseY + 0.12f : baseY; // lilypads float on the water
            go.transform.position = new Vector3(o.x + p.x, y, o.z + p.z);
            go.transform.rotation = Quaternion.Euler(0f, p.yaw, 0f);
            if (!p.col) StripColliders(go);
        }
    }

    static Transform FreshRoot(string name, out Vector3 anchor, out float baseY)
    {
        var existing = GameObject.Find(name);
        if (existing != null) Object.DestroyImmediate(existing);

        anchor = Selection.activeTransform != null
            ? Selection.activeTransform.position
            : SpawnPos() + new Vector3(70f, 0f, 130f); // default landmark spot NE of spawn
        Physics.SyncTransforms();
        baseY = GroundY(anchor);
        var go = new GameObject(name);
        go.transform.position = new Vector3(anchor.x, baseY, anchor.z);
        anchor = go.transform.position;
        return go.transform;
    }

    static void Done(Transform root, string what)
    {
        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[POI] {what} built at {root.position}. Re-run to rebuild here; select an empty elsewhere first to place another.");
    }

    static float GroundY(Vector3 at)
    {
        if (Physics.Raycast(new Vector3(at.x, 500f, at.z), Vector3.down, out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return 0f;
    }

    static Vector3 SpawnPos()
    {
        var s = Object.FindFirstObjectByType<Mirror.NetworkStartPosition>();
        return s != null ? s.transform.position : new Vector3(0, 0, -5);
    }

    static GameObject Load(string folder, string name)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".prefab");
        if (p == null) Debug.LogWarning($"[POI] Missing prefab: {folder}{name}.prefab");
        return p;
    }

    static void StripColliders(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }

    static void MeasureFootprint(GameObject go, out Vector3 size, out Vector3 center)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { size = new Vector3(4, 0, 4); center = Vector3.zero; return; }
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        size = b.size; center = b.center;
    }
}
