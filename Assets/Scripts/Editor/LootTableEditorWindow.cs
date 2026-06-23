using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LootTableEditorWindow : EditorWindow
{
    [MenuItem("Tools/Editor/Loot Table Editor")]
    static void Open() => GetWindow<LootTableEditorWindow>("Loot Table Editor");

    // ── State ─────────────────────────────────────────────────────────────────

    List<LootTable>  _tables   = new();
    LootTable        _selected;
    SerializedObject _so;
    Vector2          _listScroll;
    Vector2          _editScroll;
    string           _newName  = "New Loot Table";

    static class S
    {
        public static readonly Color Header = new(0.18f, 0.18f, 0.18f);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable() => Refresh();
    void OnFocus()  => Refresh();

    void Refresh()
    {
        _tables.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:LootTable"))
        {
            var path  = AssetDatabase.GUIDToAssetPath(guid);
            var table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
            if (table != null) _tables.Add(table);
        }
        _tables.Sort((a, b) => string.Compare(a.name, b.name));
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        DrawList();
        DrawEditor();
        EditorGUILayout.EndHorizontal();
    }

    // ── Left panel ────────────────────────────────────────────────────────────

    void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        SectionHeader("Loot Tables");

        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
        foreach (var table in _tables)
        {
            bool active = table == _selected;
            GUI.backgroundColor = active ? Color.cyan : Color.white;
            if (GUILayout.Button(table.name, EditorStyles.toolbarButton))
                Select(table);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        GUILayout.Space(4);
        _newName = EditorGUILayout.TextField(_newName);
        if (GUILayout.Button("Create New", GUILayout.Height(28)))
            CreateNew(_newName);

        EditorGUILayout.EndVertical();
    }

    // ── Right panel ───────────────────────────────────────────────────────────

    void DrawEditor()
    {
        EditorGUILayout.BeginVertical();

        if (_selected == null || _so == null)
        {
            EditorGUILayout.HelpBox("Select a loot table to edit.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        _so.Update();
        _editScroll = EditorGUILayout.BeginScrollView(_editScroll);

        DrawSection("Item Pool",    "items");
        DrawSection("Drop Counts",  "dropCounts");
        DrawSection("Coin Tiers",   "coinTiers");

        EditorGUILayout.EndScrollView();

        if (_so.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selected);

        GUILayout.Space(4);
        if (GUILayout.Button("Ping Asset", GUILayout.Height(22)))
            EditorGUIUtility.PingObject(_selected);

        EditorGUILayout.EndVertical();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void Select(LootTable table)
    {
        _selected = table;
        _so       = table != null ? new SerializedObject(table) : null;
        Repaint();
    }

    void CreateNew(string tableName)
    {
        const string dir = "Assets/ScriptableObjects/LootTables";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "LootTables");

        var table = CreateInstance<LootTable>();
        var path  = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{tableName.Trim()}.asset");
        AssetDatabase.CreateAsset(table, path);
        AssetDatabase.SaveAssets();
        Refresh();
        Select(table);
    }

    void DrawSection(string title, params string[] propNames)
    {
        SectionHeader(title);
        EditorGUI.indentLevel++;
        foreach (var propName in propNames)
        {
            var prop = _so.FindProperty(propName);
            if (prop != null)
                EditorGUILayout.PropertyField(prop, true);
        }
        EditorGUI.indentLevel--;
        GUILayout.Space(4);
    }

    static void SectionHeader(string title)
    {
        var rect  = GUILayoutUtility.GetRect(1, 20, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, S.Header);
        var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };
        style.normal.textColor = Color.white;
        EditorGUI.LabelField(new Rect(rect.x + 6, rect.y, rect.width, rect.height), title, style);
        GUILayout.Space(2);
    }
}
