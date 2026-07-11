using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 3.1.10 Stage 3 — a one-click running start for populating Creslin's Field. Two halves:
///  1. Wires the seeded example mob ids to Synty Dungeon bodies in the <see cref="MobModelCatalog"/>.
///  2. Stamps three reference encounters near the player spawn under an "Example Encounters" root — a random
///     (weighted table) spawn, a static (single named mob) spawn, and a City-Guard patrol on a looped route.
///
/// The mobs + the "Creslins Field Wildlife" table are seeded by <c>DatabaseSeeder</c> (run
/// <c>Tools/Database/Seed Database</c> or Host once). Duplicate/adjust the placed objects with the Stage-2
/// tools (Place Encounter / New Patrol Route / Add Patrol Waypoint).
/// </summary>
static class ExampleEncounterSetup
{
    const string DungeonChars = "Assets/Synty/PolygonDungeon/Prefabs/Characters/";
    const string CatalogPath  = "Assets/Resources/MobModelCatalog.asset";

    [MenuItem("Tools/Zones/Build Example Encounters (Creslins Field)")]
    static void Build()
    {
        WireExampleBodies();
        PlaceExampleEncounters();
        Debug.Log("[Example] Done. Next: Tools/Database/Seed Database (or Host once) to seed the mobs + " +
                  "wildlife table, then Play near spawn. Tweak/duplicate the 'Example Encounters' objects with " +
                  "the Stage-2 tools.");
    }

    // ── Catalog: map the example mob ids → Dungeon bodies (convention: modelId == mob id) ──────
    static void WireExampleBodies()
    {
        var cat = AssetDatabase.LoadAssetAtPath<MobModelCatalog>(CatalogPath);
        if (cat == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Resources");
            cat = ScriptableObject.CreateInstance<MobModelCatalog>();
            AssetDatabase.CreateAsset(cat, CatalogPath);
        }

        WireBody(cat, "Goblin Scout",     DungeonChars + "SM_Chr_Goblin_Male_01.prefab");
        WireBody(cat, "Skeleton Soldier", DungeonChars + "SM_Chr_Skeleton_Soldier_01.prefab");
        WireBody(cat, "Goblin Warchief",  DungeonChars + "SM_Chr_Goblin_WarChief_01.prefab");
        WireBody(cat, "City Guard",       DungeonChars + "SM_Chr_Hero_Knight_Male_01.prefab");

        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        MobModelRegistry.Invalidate();
    }

    static void WireBody(MobModelCatalog cat, string modelId, string prefabPath)
    {
        foreach (var e in cat.entries)
            if (e.modelId == modelId) return; // already mapped — leave any hand-edit intact

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Example] Prefab not found: {prefabPath} — skipping body for '{modelId}'. " +
                             "Is the PolygonDungeon pack imported?");
            return;
        }
        cat.entries.Add(new MobModelCatalog.Entry { modelId = modelId, prefab = prefab });
        Debug.Log($"[Example] Catalog: '{modelId}' → {prefab.name}");
    }

    // ── Scene: three reference encounters near spawn ───────────────────────────────────────────
    static void PlaceExampleEncounters()
    {
        if (GameObject.Find("Example Encounters") != null)
        {
            Debug.Log("[Example] 'Example Encounters' already in the scene — skipped placement (delete it to re-place).");
            return;
        }

        Vector3 spawn = FindSpawnOrigin();
        var root = new GameObject("Example Encounters");
        Undo.RegisterCreatedObjectUndo(root, "Build Example Encounters");

        // 1) Random encounter — the weighted wildlife table.
        MakeSpawn(root.transform, "Random Encounter (wildlife table)", spawn + new Vector3(25, 0, 35),
                  spawnTableId: "Creslins Field Wildlife");

        // 2) Static encounter — a single named Warchief.
        MakeSpawn(root.transform, "Static Encounter (Goblin Warchief)", spawn + new Vector3(40, 0, 10),
                  mobId: "Goblin Warchief");

        // 3) Guard patrol — a City Guard walking a looped route.
        var route = new GameObject("Guard Patrol Route");
        route.transform.SetParent(root.transform);
        route.transform.position = Snap(spawn + new Vector3(-15, 0, 10));
        var pr = route.AddComponent<PatrolRoute>();

        Vector3[] wps =
        {
            spawn + new Vector3(-15, 0, -5),
            spawn + new Vector3(-15, 0, 35),
            spawn + new Vector3( 15, 0, 35),
            spawn + new Vector3( 15, 0, -5),
        };
        for (int i = 0; i < wps.Length; i++)
        {
            var wp = new GameObject($"WP {i}");
            wp.transform.SetParent(route.transform);
            wp.transform.position = Snap(wps[i]);
        }

        var guard = MakeSpawn(root.transform, "Guard Patrol (City Guard)", spawn + new Vector3(-15, 0, 5),
                              mobId: "City Guard");
        var gSo = new SerializedObject(guard.GetComponent<SpawnPoint>());
        gSo.FindProperty("patrolRoute").objectReferenceValue = pr;
        gSo.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = root;
        EditorSceneManager_MarkDirty();
        Debug.Log("[Example] Placed 3 reference encounters near spawn under 'Example Encounters'.");
    }

    static GameObject MakeSpawn(Transform parent, string name, Vector3 pos, string spawnTableId = "", string mobId = "")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = Snap(pos);

        var sp = go.AddComponent<SpawnPoint>();
        var so = new SerializedObject(sp);
        so.FindProperty("spawnTableId").stringValue = spawnTableId;
        so.FindProperty("mobId").stringValue = mobId;
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    // Prefer the navmesh (mobs must stand there); fall back to ground colliders, then the raw point.
    static Vector3 Snap(Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out var hit, 12f, NavMesh.AllAreas)) return hit.position;
        var origin = pos + Vector3.up * 50f;
        if (Physics.Raycast(origin, Vector3.down, out var gh, 200f, ~0, QueryTriggerInteraction.Ignore)) return gh.point;
        return pos;
    }

    static Vector3 FindSpawnOrigin()
    {
        var start = Object.FindFirstObjectByType<Mirror.NetworkStartPosition>();
        return start != null ? start.transform.position : Vector3.zero;
    }

    static void EditorSceneManager_MarkDirty()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }
}
