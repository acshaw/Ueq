using UnityEditor;
using UnityEngine;

/// <summary>
/// M3.0.2 (ZA2) — Scene-view labels for zone markers so portals/waypoints are legible while authoring.
/// The geometry gizmos (portal radius sphere, waypoint facing arrow) already live on the components;
/// this adds always-on text labels above each. Editor-only.
/// </summary>
static class ZoneMarkerGizmos
{
    static GUIStyle _portalStyle;
    static GUIStyle _entryStyle;

    static GUIStyle Style(ref GUIStyle cached, Color color)
    {
        if (cached == null)
            cached = new GUIStyle { fontStyle = FontStyle.Bold, normal = { textColor = color } };
        return cached;
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
    static void DrawPortalLabel(ZonePortal p, GizmoType _)
    {
        string zone  = string.IsNullOrEmpty(p.targetZoneId)  ? "?"       : p.targetZoneId;
        string entry = string.IsNullOrEmpty(p.targetEntryId) ? "default" : p.targetEntryId;
        Handles.Label(p.transform.position + Vector3.up * (Mathf.Max(p.radius, 1f) + 1f),
            $"→ {zone}/{entry}", Style(ref _portalStyle, new Color(0.2f, 0.85f, 1f, 1f)));
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
    static void DrawEntryLabel(ZoneEntry e, GizmoType _)
    {
        string id = string.IsNullOrEmpty(e.entryId) ? "default" : e.entryId;
        Handles.Label(e.transform.position + Vector3.up * 2f,
            $"⚑ {id}", Style(ref _entryStyle, new Color(0.3f, 1f, 0.3f, 1f)));
    }
}
