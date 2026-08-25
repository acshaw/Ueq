using System.Collections.Generic;
using Npgsql;
using UnityEditor;
using UnityEngine;

/// <summary>
/// World-placement QoL (follow-up to 2.7.3) — replaces <c>SpawnPoint</c>'s free-typed <c>spawnTableId</c>/
/// <c>mobId</c> string fields with live dropdowns populated from Postgres (same
/// <see cref="Database.OpenEditorConnection"/> every other Editor tool uses), so placing a mob no longer
/// means memorizing or copy-pasting an id from a separate browser tab. The list is fetched once per Editor
/// session (or on demand via the Refresh button) — content is web-authored and can change independently of
/// this Editor session, so it's deliberately not re-queried on every repaint.
/// </summary>
[CustomEditor(typeof(SpawnPoint))]
public class SpawnPointEditor : Editor
{
    static List<string> _spawnTableIds;
    static List<string> _mobIds;
    static string _fetchError;

    void OnEnable()
    {
        if (_spawnTableIds == null || _mobIds == null)
            Refresh();
    }

    static void Refresh()
    {
        _fetchError = null;
        try
        {
            using var conn = Database.OpenEditorConnection();
            _spawnTableIds = QueryIds(conn, "SELECT spawn_table_id FROM spawn_tables ORDER BY spawn_table_id");
            _mobIds        = QueryIds(conn, "SELECT mob_id FROM mobs ORDER BY mob_id");
        }
        catch (System.Exception e)
        {
            _fetchError    = e.Message;
            _spawnTableIds ??= new List<string>();
            _mobIds        ??= new List<string>();
        }
    }

    static List<string> QueryIds(NpgsqlConnection conn, string sql)
    {
        var ids = new List<string>();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            ids.Add(reader.GetString(0));
        return ids;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Spawn Source", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Pick what spawns here from the database — the same ids the web Mob/Spawn editors use. " +
            "A Spawn Table takes precedence over a single Mob if both are set.", MessageType.None);
        if (GUILayout.Button("Refresh from Database", GUILayout.Width(180)))
            Refresh();
        if (!string.IsNullOrEmpty(_fetchError))
            EditorGUILayout.HelpBox($"Couldn't reach the database: {_fetchError}", MessageType.Warning);

        DrawIdDropdown("spawnTableId", "Spawn Table", _spawnTableIds);
        DrawIdDropdown("mobId", "Mob (single)", _mobIds);
        EditorGUILayout.Space();

        DrawPropertiesExcluding(serializedObject, "m_Script", "spawnTableId", "mobId", "placementId");

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("placementId"), new GUIContent("Placement Id"));

        serializedObject.ApplyModifiedProperties();
    }

    // Renders propName as a dropdown of knownIds instead of a free-typed string. A current value that
    // isn't in the fetched list (renamed/removed on the web, or the list is just stale) is shown flagged
    // rather than silently dropped, so a working reference is never clobbered by an out-of-date list.
    void DrawIdDropdown(string propName, string label, List<string> knownIds)
    {
        var prop = serializedObject.FindProperty(propName);
        string current = prop.stringValue;
        knownIds ??= new List<string>();

        var options = new List<string> { "(none)" };
        options.AddRange(knownIds);

        int index = string.IsNullOrEmpty(current) ? 0 : options.IndexOf(current);
        bool stale = !string.IsNullOrEmpty(current) && index < 0;
        if (stale)
        {
            options.Add($"{current}  (not found — stale?)");
            index = options.Count - 1;
        }

        int newIndex = EditorGUILayout.Popup(label, index, options.ToArray());
        if (newIndex != index)
            prop.stringValue = newIndex == 0 ? "" : options[newIndex];

        if (stale)
            EditorGUILayout.HelpBox(
                $"'{current}' wasn't found in the database — it may have been renamed/removed on the web, " +
                "or this list is just out of date. Click Refresh, or pick a new value above.", MessageType.Warning);
    }
}
