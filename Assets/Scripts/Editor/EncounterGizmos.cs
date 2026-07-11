using UnityEditor;
using UnityEngine;

/// <summary>
/// 3.1.10 Stage 2 — always-on Scene-view labels for encounter authoring, matching <c>ZoneMarkerGizmos</c>.
/// SpawnPoints show what they spawn (+ a patrol flag); patrol routes show their point count/mode and number
/// each waypoint. Geometry gizmos (activation radius, route polyline) live on the components themselves.
/// </summary>
static class EncounterGizmos
{
    static GUIStyle _spawnStyle, _routeStyle, _wpStyle, _regionStyle;

    static GUIStyle Style(ref GUIStyle cached, Color color)
    {
        if (cached == null)
            cached = new GUIStyle { fontStyle = FontStyle.Bold, normal = { textColor = color } };
        return cached;
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
    static void DrawSpawnLabel(SpawnPoint s, GizmoType _)
    {
        string what = !string.IsNullOrEmpty(s.SpawnTableId) ? $"table:{s.SpawnTableId}"
                    : !string.IsNullOrEmpty(s.MobId)         ? $"mob:{s.MobId}"
                    : "(unset)";
        if (s.HasPatrol)             what += " · patrol";
        else if (s.HasWanderRegion)  what += " · region";
        else if (s.FreeRange)        what += " · free";

        Handles.Label(s.transform.position + Vector3.up * 2f, $"◆ {what}",
            Style(ref _spawnStyle, new Color(1f, 0.85f, 0.2f, 1f)));
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
    static void DrawWanderRegionLabel(WanderRegion w, GizmoType _)
    {
        Handles.Label(w.transform.position + Vector3.up * 2f, $"▨ wander region ({w.shape})",
            Style(ref _regionStyle, new Color(0.3f, 1f, 0.5f, 1f)));
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
    static void DrawRouteLabels(PatrolRoute r, GizmoType _)
    {
        int n = r.transform.childCount;
        Handles.Label(r.transform.position + Vector3.up * 2f,
            $"⚑ route ({n} pt{(n == 1 ? "" : "s")}, {(r.loop ? "loop" : "ping-pong")})",
            Style(ref _routeStyle, new Color(0.2f, 0.7f, 1f, 1f)));

        for (int i = 0; i < n; i++)
            Handles.Label(r.transform.GetChild(i).position + Vector3.up * 0.6f, i.ToString(),
                Style(ref _wpStyle, new Color(0.6f, 0.85f, 1f, 1f)));
    }
}
