using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 3.5 — populates Thornwood with its signature encounter design (per <c>trellis_zone_design.md</c>'s
/// "Zone 2: Thornwood"): goblin patrols in the southern band + an organized patrol nearer the Grukmar's
/// Deep notch, plus a solitary wandering scout between them. Mirrors <see cref="ExampleEncounterSetup"/>'s
/// two-half shape (wire bodies into the <see cref="MobModelCatalog"/>, then place scene content) using the
/// same Stage-2 primitives (<c>PatrolRoute</c>/<c>WanderRegion</c>/<c>SpawnPoint</c>) 3.1.10/3.1.11 already
/// built — nothing new at the engine level.
///
/// Run AFTER <c>Tools/Zones/Build Thornwood Terrain</c> (needs the real terrain surface to snap onto) and
/// AFTER <c>Tools/Database/Seed Database</c> has run at least once (needs the "Goblin Scout" mob to exist).
/// Removes the 3.0.2 placeholder "RatSpawn" (a Giant Rat never fit this zone's design). Idempotent: skips
/// placement if "Thornwood Encounters" already exists (delete it to re-place).
/// </summary>
static class ThornwoodEncounterSetup
{
    const string DungeonChars = "Assets/Synty/PolygonDungeon/Prefabs/Characters/";
    const string CatalogPath  = "Assets/Resources/MobModelCatalog.asset";

    [MenuItem("Tools/Zones/Build Thornwood Encounters")]
    static void Build()
    {
        WireBodies();
        RemoveLegacyRatSpawn();
        PlaceEncounters();
        Debug.Log("[ThornwoodEncounters] Done. NEXT: in the web Mob Editor, raise 'Goblin Scout' to level " +
                  "6-9 for Thornwood's southern-treeline band (it's shared with Creslin's Field, so this is " +
                  "a manual edit rather than a re-seed — seeding never overwrites web-authored data). Then " +
                  "Tools/Database/Seed Database (or Host once) to pick up 'Goblin Warrior' + the two patrol " +
                  "tables, rebake the navmesh if you moved anything, and SAVE the scene.");
    }

    // ── Catalog: wire the Thornwood-relevant mob ids → Dungeon bodies ───────────────────────────
    static void WireBodies()
    {
        var cat = AssetDatabase.LoadAssetAtPath<MobModelCatalog>(CatalogPath);
        if (cat == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Resources");
            cat = ScriptableObject.CreateInstance<MobModelCatalog>();
            AssetDatabase.CreateAsset(cat, CatalogPath);
        }

        WireBody(cat, "Goblin Scout",   DungeonChars + "SM_Chr_Goblin_Male_01.prefab");          // defensive — 3.1.10 usually already wires this
        WireBody(cat, "Goblin Warrior", DungeonChars + "SM_Chr_Goblin_Warrior_Male_01.prefab");

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
            Debug.LogWarning($"[ThornwoodEncounters] Prefab not found: {prefabPath} — skipping body for '{modelId}'.");
            return;
        }
        cat.entries.Add(new MobModelCatalog.Entry { modelId = modelId, prefab = prefab });
        Debug.Log($"[ThornwoodEncounters] Catalog: '{modelId}' → {prefab.name}");
    }

    static void RemoveLegacyRatSpawn()
    {
        var go = GameObject.Find("RatSpawn");
        if (go == null) return;
        Undo.DestroyObjectImmediate(go);
        Debug.Log("[ThornwoodEncounters] Removed the 3.0.2 placeholder 'RatSpawn' (didn't fit this zone's goblin-forest design).");
    }

    // ── Scene: two patrol bands + a solitary wanderer ────────────────────────────────────────────
    static void PlaceEncounters()
    {
        if (GameObject.Find("Thornwood Encounters") != null)
        {
            Debug.Log("[ThornwoodEncounters] 'Thornwood Encounters' already in the scene — skipped placement (delete it to re-place).");
            return;
        }

        Vector3 origin = FindDefaultEntryOrigin();
        var root = new GameObject("Thornwood Encounters");
        Undo.RegisterCreatedObjectUndo(root, "Build Thornwood Encounters");

        // Southern-treeline band: mixed scouts (per the doc — one body for MVP, see the 3.5 devplan TW5/TW6)
        // moving in a loose group of 3 along a beat just past the arrival clearing.
        var southRoute = BuildRoute(root, "Patrol Route (South Treeline)", origin, new[]
        {
            new Vector3(-40f, 0f,  70f),
            new Vector3( 45f, 0f, 100f),
            new Vector3( 30f, 0f, 170f),
            new Vector3(-35f, 0f, 145f),
        });
        var southSpawn = BuildSpawnPoint(root, "Encounter (South Patrol)", SnapToSurface(origin + new Vector3(0f, 0f, 110f)));
        WireSpawnTable(southSpawn, "Thornwood Goblin Patrol");
        WireRoute(southSpawn, southRoute);

        // Organized warrior patrol nearer the Grukmar's Deep entrance ("closer to the dungeon entrance,
        // organized patrol routes" per the doc) — placed toward the east-wall notch direction.
        var approachRoute = BuildRoute(root, "Patrol Route (Approach)", origin, new[]
        {
            new Vector3(-25f, 0f, 420f),
            new Vector3( 70f, 0f, 460f),
            new Vector3( 55f, 0f, 540f),
            new Vector3(-45f, 0f, 500f),
        });
        var approachSpawn = BuildSpawnPoint(root, "Encounter (Approach Patrol)", SnapToSurface(origin + new Vector3(0f, 0f, 480f)));
        WireSpawnTable(approachSpawn, "Thornwood Warrior Patrol");
        WireRoute(approachSpawn, approachRoute);

        // A solitary scout roaming the mid-forest between the two patrol bands (so it doesn't read as
        // only patrol lines).
        Vector3 wanderCenter = SnapToSurface(origin + new Vector3(130f, 0f, 280f));
        var region = new GameObject("Wander Region (Mid Forest)");
        Undo.RegisterCreatedObjectUndo(region, "Build Thornwood Encounters");
        region.transform.SetParent(root.transform, true);
        region.transform.position = wanderCenter;
        var wr = region.AddComponent<WanderRegion>();
        wr.shape = WanderRegion.Shape.Box;
        wr.boxSize = new Vector3(160f, 20f, 160f);
        wr.sampleRadius = 10f; // rolling terrain varies more than the flat 3.0.2 scaffold this replaces

        var wanderSpawn = BuildSpawnPoint(root, "Encounter (Wandering Scout)", wanderCenter);
        WireMobId(wanderSpawn, "Goblin Scout");
        WireWanderRegion(wanderSpawn, wr);

        Selection.activeGameObject = root;
        Debug.Log("[ThornwoodEncounters] Placed 2 patrol bands + 1 wandering scout under 'Thornwood Encounters'. " +
                  "Adjust/duplicate with the Stage-2 tools (Place Encounter / Add Patrol Waypoint).");
    }

    static PatrolRoute BuildRoute(GameObject root, string name, Vector3 origin, Vector3[] localOffsets)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Thornwood Encounters");
        go.transform.SetParent(root.transform, true);
        go.transform.position = origin + localOffsets[0];
        var route = go.AddComponent<PatrolRoute>();

        foreach (var offset in localOffsets)
        {
            var wp = new GameObject($"WP {go.transform.childCount}");
            wp.transform.SetParent(go.transform, worldPositionStays: true);
            wp.transform.position = SnapToSurface(origin + offset);
        }
        return route;
    }

    // NavMesh sample first (generous radius — the rolling terrain can differ from the flat authored offset
    // by well more than a few units), falling back to a straight-down raycast.
    static Vector3 SnapToSurface(Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out var navHit, 40f, NavMesh.AllAreas))
            return navHit.position;

        var rayOrigin = pos + Vector3.up * 300f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, 600f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;

        return pos;
    }

    static SpawnPoint BuildSpawnPoint(GameObject root, string name, Vector3 worldPos)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Thornwood Encounters");
        go.transform.SetParent(root.transform, true);
        go.transform.position = worldPos;
        return go.AddComponent<SpawnPoint>();
    }

    static void WireSpawnTable(SpawnPoint sp, string spawnTableId)
    {
        var so = new SerializedObject(sp);
        var prop = so.FindProperty("spawnTableId");
        if (prop != null) prop.stringValue = spawnTableId;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireMobId(SpawnPoint sp, string mobId)
    {
        var so = new SerializedObject(sp);
        var prop = so.FindProperty("mobId");
        if (prop != null) prop.stringValue = mobId;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireRoute(SpawnPoint sp, PatrolRoute route)
    {
        var so = new SerializedObject(sp);
        var prop = so.FindProperty("patrolRoute");
        if (prop != null) prop.objectReferenceValue = route;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireWanderRegion(SpawnPoint sp, WanderRegion region)
    {
        var so = new SerializedObject(sp);
        var prop = so.FindProperty("wanderRegion");
        if (prop != null) prop.objectReferenceValue = region;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Vector3 FindDefaultEntryOrigin()
    {
        foreach (var e in Object.FindObjectsByType<ZoneEntry>(FindObjectsSortMode.None))
            if (e.entryId == "default") return e.transform.position;

        Debug.LogWarning("[ThornwoodEncounters] No ZoneEntry(\"default\") found — falling back to the 3.0.2 scaffold's known position.");
        return new Vector3(5000f, 0f, -8f);
    }
}
