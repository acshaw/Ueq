using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 3.1.10 Stage 2 — in-scene placement tooling for encounters + patrols, mirroring the zone-authoring tools.
/// Drops configured GameObjects at the Scene-view focus, snapped to the navmesh (so mobs can actually stand
/// there). Scene-view labels for what's placed live in <c>EncounterGizmos</c>.
/// </summary>
static class EncounterTools
{
    [MenuItem("Tools/Zones/Place Encounter (Spawn Point)")]
    static void PlaceEncounter()
    {
        var go = new GameObject("Encounter (SpawnPoint)");
        go.transform.position = SnapToNavmeshOrGround(SceneFocus());
        go.AddComponent<SpawnPoint>();

        Undo.RegisterCreatedObjectUndo(go, "Place Encounter");
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        Debug.Log("[Encounter] Placed a SpawnPoint. In the Inspector set its spawnTableId (weighted DB table) " +
                  "or mobId (single named mob), and optionally a Patrol Route.");
    }

    [MenuItem("Tools/Zones/New Patrol Route")]
    static void NewPatrolRoute()
    {
        var go = new GameObject("Patrol Route");
        go.transform.position = SnapToNavmeshOrGround(SceneFocus());
        go.AddComponent<PatrolRoute>();

        Undo.RegisterCreatedObjectUndo(go, "New Patrol Route");
        Selection.activeGameObject = go;
        Debug.Log("[Encounter] Created a Patrol Route. With it selected, use Tools/Zones/Add Patrol Waypoint " +
                  "(move the Scene camera between adds), then set a SpawnPoint's Patrol Route to it.");
    }

    [MenuItem("Tools/Zones/Add Patrol Waypoint")]
    static void AddPatrolWaypoint()
    {
        var route = ResolveSelectedRoute();
        if (route == null)
        {
            Debug.LogWarning("[Encounter] Select a Patrol Route (or one of its waypoints) first.");
            return;
        }

        var wp = new GameObject($"WP {route.transform.childCount}");
        wp.transform.SetParent(route.transform, worldPositionStays: true);
        wp.transform.position = SnapToNavmeshOrGround(SceneFocus());

        Undo.RegisterCreatedObjectUndo(wp, "Add Patrol Waypoint");
        Selection.activeGameObject = route.gameObject; // keep the route selected so you can keep adding
        EditorGUIUtility.PingObject(wp);
    }

    // Only enabled when a Patrol Route (or a child of one) is selected.
    [MenuItem("Tools/Zones/Add Patrol Waypoint", true)]
    static bool AddPatrolWaypointValidate() => ResolveSelectedRoute() != null;

    [MenuItem("Tools/Zones/New Wander Region")]
    static void NewWanderRegion()
    {
        var go = new GameObject("Wander Region");
        go.transform.position = SnapToNavmeshOrGround(SceneFocus());
        go.AddComponent<WanderRegion>();

        Undo.RegisterCreatedObjectUndo(go, "New Wander Region");
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        Debug.Log("[Encounter] Created a Wander Region. Set its Box/Sphere shape + size in the Inspector, then " +
                  "assign it to a SpawnPoint's Wander Region field (constrains wander mobs to this area).");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static PatrolRoute ResolveSelectedRoute()
    {
        var sel = Selection.activeGameObject;
        if (sel == null) return null;
        return sel.GetComponent<PatrolRoute>() ?? sel.GetComponentInParent<PatrolRoute>();
    }

    static Vector3 SceneFocus()
    {
        var sv = SceneView.lastActiveSceneView;
        return sv != null ? sv.pivot : Vector3.zero;
    }

    // Prefer the navmesh (mobs must be able to stand there); fall back to ground colliders, then the raw point.
    static Vector3 SnapToNavmeshOrGround(Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out var navHit, 10f, NavMesh.AllAreas))
            return navHit.position;

        var origin = pos + Vector3.up * 50f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;

        return pos;
    }
}
