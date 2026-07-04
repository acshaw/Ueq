using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// M3.0.2 — zone authoring tooling. Builds the 3 MVP zones (Creslin's Field ↔ Thornwood ↔ Grukmar's Deep)
/// as appropriately-sized flat scaffolds wired into a linear portal graph, and exposes the generic
/// <see cref="BuildFlatZone"/> / <see cref="CreateZone"/> used by <c>Tools/Zones/New Zone…</c>.
///
///   • <c>Tools/Zones/Build Zone Scenes</c> — build/refresh all 3 MVP zones + the portal graph.
///   • <c>Tools/Zones/New Zone…</c>        — stamp a new flat zone (see <see cref="NewZoneWindow"/>).
///   • <c>Tools/Zones/Create Zone Prefabs</c> — (re)create the drag-in ZonePortal / ZoneWaypoint prefabs.
///
/// Zones are authored AT their world offset (Z3-B); the baked NavMesh is persisted as an asset so it
/// reloads with the additive scene (3.0.1 fix). The base scene (Creslin's Field = the active SampleScene,
/// decision A) is never regenerated — only its portal/entry markers are placed. Idempotent: re-running
/// rebuilds each non-base zone in place and upserts the catalog.
/// </summary>
public static class ZoneSetup
{
    // ── MVP zone identities ───────────────────────────────────────────────────────
    const string StarterZoneId   = "creslins_field";
    const string ThornZoneId     = "thornwood";
    const string GrukmarZoneId    = "grukmars_deep";
    const string ThornSceneName   = "thornwood";
    const string GrukmarSceneName = "grukmars_deep";

    static readonly Vector3 ThornOffset   = new Vector3(5000f, 0f, 0f);
    static readonly Vector3 GrukmarOffset = new Vector3(10000f, 0f, 0f);

    // ZA4: ~3–5 min walk at the current 1 u/s walk speed ≈ 180–300 units across. Flat + hand-decorated later.
    const float MvpZoneSize = 280f;

    // ── Paths ─────────────────────────────────────────────────────────────────────
    const string ZonesDir           = "Assets/Scenes/Zones";
    const string CatalogPath        = "Assets/Resources/ZoneCatalog.asset";
    const string ZonePrefabDir      = "Assets/Prefabs/Zones";
    const string PortalPrefabPath   = "Assets/Prefabs/Zones/ZonePortal.prefab";
    const string WaypointPrefabPath = "Assets/Prefabs/Zones/ZoneWaypoint.prefab";

    static string ScenePath(string sceneName)   => $"{ZonesDir}/{sceneName}.unity";
    static string NavMeshPath(string sceneName) => $"{ZonesDir}/{sceneName}_navmesh.asset";

    // ── Build the 3 MVP zones + portal graph ───────────────────────────────────────

    [MenuItem("Tools/Zones/Build Zone Scenes")]
    public static void BuildZoneScenes()
    {
        EnsureZonePrefabs();
        WireNetworkManager();
        PlaceCreslinsMarkers();

        var catalog = LoadOrCreateCatalog();
        catalog.starterZoneId = StarterZoneId;
        UpsertZone(catalog, new ZoneDefinition
        {
            zoneId = StarterZoneId, sceneName = SceneManager.GetActiveScene().name,
            worldOffset = Vector3.zero, isBaseScene = true,
        });

        // Thornwood — arrives from creslins at "default"; links back to creslins + on to grukmar.
        BuildFlatZone(ThornZoneId, ThornSceneName, ThornOffset, MvpZoneSize, root =>
        {
            AddEntry (root, "default",      ThornOffset + new Vector3(0f, 0f, -8f),   0f);  // arrive facing +Z
            AddEntry (root, "from_grukmar", ThornOffset + new Vector3(0f, 0f,  8f), 180f);  // arrive facing -Z
            AddPortal(root, "Portal_To_Creslins", ThornOffset + new Vector3(0f, 0f, -16f), StarterZoneId, "from_thornwood", 3f);
            AddPortal(root, "Portal_To_Grukmar",  ThornOffset + new Vector3(0f, 0f,  16f), GrukmarZoneId,  "default",        3f);
            AddRatSpawn(root, ThornOffset + new Vector3(6f, 0f, 0f));
        });
        UpsertZone(catalog, new ZoneDefinition { zoneId = ThornZoneId, sceneName = ThornSceneName, worldOffset = ThornOffset });

        // Grukmar's Deep — arrives from thornwood at "default"; links back to thornwood. (Dungeon dressing = 3.1.)
        BuildFlatZone(GrukmarZoneId, GrukmarSceneName, GrukmarOffset, MvpZoneSize, root =>
        {
            AddEntry (root, "default", GrukmarOffset + new Vector3(0f, 0f, -8f), 0f);
            AddPortal(root, "Portal_To_Thornwood", GrukmarOffset + new Vector3(0f, 0f, -16f), ThornZoneId, "from_grukmar", 3f);
            AddRatSpawn(root, GrukmarOffset + new Vector3(6f, 0f, 0f));
        });
        UpsertZone(catalog, new ZoneDefinition { zoneId = GrukmarZoneId, sceneName = GrukmarSceneName, worldOffset = GrukmarOffset });

        SaveCatalog(catalog);
        AddSceneToBuildSettings(ScenePath(ThornSceneName));
        AddSceneToBuildSettings(ScenePath(GrukmarSceneName));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[ZoneSetup] Built 3 MVP zones (creslins ↔ thornwood ↔ grukmars_deep). NEXT: SAVE the active " +
                  "scene (Ctrl+S) — it gained the ZoneManager components + creslins portal/entry. Navmesh is " +
                  "baked + persisted per zone; if any looks unbaked, open it, select ZoneRoot, Bake, save.");
    }

    // ── Generic single-zone creation (used by Tools/Zones/New Zone…) ────────────────

    /// <summary>Create one flat zone + register it in the catalog + Build Settings. Adds a single
    /// <c>default</c> entry; the designer places portals/entries by hand with the drag-in prefabs.</summary>
    public static void CreateZone(string zoneId, string sceneName, Vector3 offset, float groundSize)
    {
        if (string.IsNullOrWhiteSpace(zoneId) || string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[ZoneSetup] Zone id and scene name are required."); return;
        }
        if (zoneId == StarterZoneId)
        {
            Debug.LogError("[ZoneSetup] The starter zone is the base scene and can't be regenerated."); return;
        }

        EnsureZonePrefabs();
        BuildFlatZone(zoneId, sceneName, offset, groundSize, root => AddEntry(root, "default", offset, 0f));

        var catalog = LoadOrCreateCatalog();
        UpsertZone(catalog, new ZoneDefinition { zoneId = zoneId, sceneName = sceneName, worldOffset = offset });
        SaveCatalog(catalog);
        AddSceneToBuildSettings(ScenePath(sceneName));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ZoneSetup] Created zone '{zoneId}' ({groundSize}u @ {offset}). Place ZonePortal / " +
                  "ZoneWaypoint prefabs to wire it, then save the scene.");
    }

    /// <summary>Build a flat zone scene (ground + collider + persisted navmesh + a <paramref name="decorate"/>
    /// hook for entries/portals/spawns) at a world offset, saved to <c>Assets/Scenes/Zones/&lt;scene&gt;.unity</c>.</summary>
    public static void BuildFlatZone(string zoneId, string sceneName, Vector3 offset, float groundSize,
                                     System.Action<GameObject> decorate)
    {
        Directory.CreateDirectory(ZonesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        // Root carries the NavMeshSurface (Children collection → bakes only this zone's ground).
        var root = new GameObject("ZoneRoot");
        root.transform.position = offset;
        SceneManager.MoveGameObjectToScene(root, scene);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = zoneId + "_Ground";
        ground.transform.SetParent(root.transform, false);
        ground.transform.localScale = new Vector3(groundSize / 10f, 1f, groundSize / 10f); // Plane is 10u at scale 1

        BakeAndPersistNavMesh(root, sceneName);

        decorate?.Invoke(root);

        var path = ScenePath(sceneName);
        EditorSceneManager.SaveScene(scene, path);
        EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"[ZoneSetup] Built flat zone '{zoneId}' ({groundSize}u) → {path}.");
    }

    // ── NavMesh (baked + persisted so it reloads with the additive scene, 3.0.1) ────

    static void BakeAndPersistNavMesh(GameObject root, string sceneName)
    {
        var surfaceType = FindType("Unity.AI.Navigation.NavMeshSurface");
        if (surfaceType == null)
        {
            Debug.LogWarning("[ZoneSetup] AI Navigation package missing — add a NavMeshSurface to ZoneRoot and bake manually.");
            return;
        }

        var surface = root.AddComponent(surfaceType);
        var so = new SerializedObject(surface);
        SetEnum(so, "m_CollectObjects", "Children");      // only this root's children
        SetEnum(so, "m_UseGeometry", "PhysicsColliders"); // bake off the plane's collider
        so.ApplyModifiedPropertiesWithoutUndo();

        var build = surfaceType.GetMethod("BuildNavMesh");
        if (build != null) build.Invoke(surface, null);

        // The scripted BuildNavMesh leaves the NavMeshData in memory; if it isn't saved as an asset the
        // additive scene reloads with NO navmesh at the offset → mobs spawn off-navmesh and can't wander.
        var surfSo  = new SerializedObject(surface);
        var dataRef = surfSo.FindProperty("m_NavMeshData");
        var data    = dataRef?.objectReferenceValue;
        string navPath = NavMeshPath(sceneName);
        if (data != null)
        {
            if (!AssetDatabase.Contains(data))
            {
                AssetDatabase.DeleteAsset(navPath);
                AssetDatabase.CreateAsset(data, navPath);
            }
            dataRef.objectReferenceValue = data;
            surfSo.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[ZoneSetup] Navmesh baked + persisted → {navPath}.");
        }
        else Debug.LogWarning($"[ZoneSetup] {sceneName} BuildNavMesh produced no data — bake ZoneRoot's NavMeshSurface manually, then save.");
    }

    // ── Marker placement (prefab-based) ─────────────────────────────────────────────

    static void AddEntry(GameObject parent, string entryId, Vector3 worldPos, float yaw)
    {
        var go = InstantiateZonePrefab(WaypointPrefabPath, "Waypoint_" + entryId, parent.transform);
        go.transform.position = worldPos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var e = go.GetComponent<ZoneEntry>() ?? go.AddComponent<ZoneEntry>();
        e.entryId = entryId;
    }

    static void AddPortal(GameObject parent, string name, Vector3 worldPos, string targetZone, string targetEntry, float radius)
    {
        var go = InstantiateZonePrefab(PortalPrefabPath, name, parent.transform);
        go.transform.position = worldPos;
        var p = go.GetComponent<ZonePortal>() ?? go.AddComponent<ZonePortal>();
        p.targetZoneId = targetZone; p.targetEntryId = targetEntry; p.radius = radius;
    }

    static void AddRatSpawn(GameObject parent, Vector3 worldPos)
    {
        var go = new GameObject("RatSpawn");
        go.transform.SetParent(parent.transform, true);
        go.transform.position = worldPos;
        var sp = go.AddComponent<SpawnPoint>();
        var so = new SerializedObject(sp);
        var mob = so.FindProperty("mobId");
        if (mob != null) mob.stringValue = "Giant Rat";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Instantiate a zone prefab (falls back to a bare GO if the prefab is missing) and parent it, keeping
    // world position so a subsequent transform.position set is unambiguous.
    static GameObject InstantiateZonePrefab(string prefabPath, string name, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : new GameObject(name);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, true);
        return go;
    }

    // ── Creslin's Field (base scene) markers ────────────────────────────────────────

    static void PlaceCreslinsMarkers()
    {
        EnsurePortal("Portal_To_Thornwood", new Vector3(0f, 0f, 10f), ThornZoneId, "default", 3f);
        EnsureEntry ("Entry_From_Thornwood", new Vector3(0f, 0f, 0f), "from_thornwood");
    }

    static void EnsurePortal(string goName, Vector3 pos, string targetZone, string entryId, float radius)
    {
        var go = GameObject.Find(goName) ?? InstantiateZonePrefab(PortalPrefabPath, goName, null);
        go.name = goName;
        go.transform.position = pos;
        var p = go.GetComponent<ZonePortal>() ?? go.AddComponent<ZonePortal>();
        p.targetZoneId = targetZone; p.targetEntryId = entryId; p.radius = radius;
    }

    static void EnsureEntry(string goName, Vector3 pos, string entryId)
    {
        var go = GameObject.Find(goName) ?? InstantiateZonePrefab(WaypointPrefabPath, goName, null);
        go.name = goName;
        go.transform.position = pos;
        var e = go.GetComponent<ZoneEntry>() ?? go.AddComponent<ZoneEntry>();
        e.entryId = entryId;
    }

    // ── Drag-in prefabs ─────────────────────────────────────────────────────────────

    [MenuItem("Tools/Zones/Create Zone Prefabs")]
    public static void EnsureZonePrefabs()
    {
        Directory.CreateDirectory(ZonePrefabDir);
        CreatePrefabIfMissing(PortalPrefabPath,   "ZonePortal",   go => go.AddComponent<ZonePortal>());
        CreatePrefabIfMissing(WaypointPrefabPath, "ZoneWaypoint", go => go.AddComponent<ZoneEntry>());
    }

    static void CreatePrefabIfMissing(string path, string name, System.Action<GameObject> build)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var go = new GameObject(name);
        build(go);
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log($"[ZoneSetup] Created prefab {path}.");
    }

    // ── NetworkManager wiring ───────────────────────────────────────────────────────

    static void WireNetworkManager()
    {
        var nm = Object.FindAnyObjectByType<GameNetworkManager>();
        if (nm == null) { Debug.LogWarning("[ZoneSetup] No GameNetworkManager in the active scene — skipping NM wiring."); return; }

        if (nm.GetComponent<ZoneManager>() == null) nm.gameObject.AddComponent<ZoneManager>();
        if (nm.GetComponent<ZoneInterestManagement>() == null) nm.gameObject.AddComponent<ZoneInterestManagement>();
        Debug.Log("[ZoneSetup] NetworkManager wired with ZoneManager + ZoneInterestManagement.");
    }

    // ── Catalog + Build Settings ────────────────────────────────────────────────────

    static ZoneCatalog LoadOrCreateCatalog()
    {
        Directory.CreateDirectory("Assets/Resources");
        var catalog = AssetDatabase.LoadAssetAtPath<ZoneCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ZoneCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            Debug.Log($"[ZoneSetup] Created {CatalogPath}.");
        }
        return catalog;
    }

    static void SaveCatalog(ZoneCatalog catalog)
    {
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    // Replace-or-add a zone by id (preserves any zones not touched by this run).
    static void UpsertZone(ZoneCatalog catalog, ZoneDefinition def)
    {
        catalog.zones.RemoveAll(z => z == null || z.zoneId == def.zoneId);
        catalog.zones.Add(def);
    }

    static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in scenes) if (s.path == scenePath) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[ZoneSetup] Added {scenePath} to Build Settings.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    static void SetEnum(SerializedObject so, string prop, string enumName)
    {
        var p = so.FindProperty(prop);
        if (p == null) return;
        int idx = System.Array.IndexOf(p.enumNames, enumName);
        if (idx >= 0) p.enumValueIndex = idx;
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
