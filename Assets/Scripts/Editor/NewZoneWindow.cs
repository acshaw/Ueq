using UnityEditor;
using UnityEngine;

/// <summary>
/// M3.0.2 — <c>Tools/Zones/New Zone…</c>. Stamps a new flat zone scene at a world offset (ground + persisted
/// navmesh + a "default" entry), registers it in the ZoneCatalog + Build Settings via
/// <see cref="ZoneSetup.CreateZone"/>. The designer then places ZonePortal / ZoneWaypoint prefabs by hand
/// to wire it into the graph.
/// </summary>
public class NewZoneWindow : EditorWindow
{
    string  _zoneId    = "new_zone";
    string  _sceneName = "new_zone";
    Vector3 _offset    = new Vector3(15000f, 0f, 0f);
    float   _groundSize = 280f;

    [MenuItem("Tools/Zones/New Zone…")]
    static void Open() => GetWindow<NewZoneWindow>(true, "New Zone");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Create a flat zone scaffold", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _zoneId     = EditorGUILayout.TextField(new GUIContent("Zone Id", "Stable id stored in ZoneCatalog + characters.zone_id (e.g. thornwood)."), _zoneId);
        _sceneName  = EditorGUILayout.TextField(new GUIContent("Scene Name", "Scene file name (no path/extension) under Assets/Scenes/Zones/."), _sceneName);
        _offset     = EditorGUILayout.Vector3Field(new GUIContent("World Offset", "Authoring offset. Keep zones ≥ ~5000 apart and offset > ground size so they never bleed."), _offset);
        _groundSize = EditorGUILayout.FloatField(new GUIContent("Ground Size (units)", "Square ground size. ~280u ≈ a 3–5 min walk at the current 1 u/s walk speed."), _groundSize);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Builds a flat ground + baked/persisted navmesh + a 'default' arrival entry, then registers the " +
            "zone in ZoneCatalog and Build Settings. Wire it into the world by dragging the ZonePortal / " +
            "ZoneWaypoint prefabs into this zone's scene (and a return portal into the neighbouring zone), " +
            "then save the scene(s).",
            MessageType.Info);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_zoneId) || string.IsNullOrWhiteSpace(_sceneName)))
        {
            if (GUILayout.Button("Create Zone", GUILayout.Height(28)))
            {
                ZoneSetup.CreateZone(_zoneId.Trim(), _sceneName.Trim(), _offset, Mathf.Max(20f, _groundSize));
                Close();
            }
        }
    }
}
