using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AbilityTagEditorWindow : EditorWindow
{
    [MenuItem("Tools/Editor/Ability Tag Editor")]
    static void Open() => GetWindow<AbilityTagEditorWindow>("Ability Tag Editor");

    List<AbilityTag> _tags = new();
    AbilityTag       _selected;
    SerializedObject _so;
    Vector2          _listScroll;
    string           _newName = "New Tag";

    static class S { public static readonly Color Header = new(0.18f, 0.18f, 0.18f); }

    void OnEnable() => Refresh();
    void OnFocus()  => Refresh();

    void Refresh()
    {
        _tags.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:AbilityTag"))
        {
            var tag = AssetDatabase.LoadAssetAtPath<AbilityTag>(AssetDatabase.GUIDToAssetPath(guid));
            if (tag != null) _tags.Add(tag);
        }
        _tags.Sort((a, b) => string.Compare(a.displayName, b.displayName));
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        DrawList();
        DrawEditor();
        EditorGUILayout.EndHorizontal();
    }

    void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        SectionHeader("Ability Tags");

        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
        foreach (var tag in _tags)
        {
            bool active = tag == _selected;
            GUI.backgroundColor = active ? Color.cyan : Color.white;
            if (GUILayout.Button(string.IsNullOrEmpty(tag.displayName) ? tag.tagId : tag.displayName,
                    EditorStyles.toolbarButton))
                Select(tag);
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

    void DrawEditor()
    {
        EditorGUILayout.BeginVertical();
        if (_selected == null || _so == null)
        {
            EditorGUILayout.HelpBox("Select a tag to edit.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        _so.Update();

        SectionHeader("Tag");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_so.FindProperty("tagId"),      new GUIContent("Tag ID"));
        EditorGUILayout.PropertyField(_so.FindProperty("displayName"), new GUIContent("Display Name"));
        EditorGUI.indentLevel--;

        GUILayout.Space(4);

        if (_so.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selected);

        GUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ping Asset", GUILayout.Height(22)))
            EditorGUIUtility.PingObject(_selected);

        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button("Delete", GUILayout.Height(22)) &&
            EditorUtility.DisplayDialog("Delete Tag",
                $"Delete '{_selected.displayName}'? This cannot be undone.", "Delete", "Cancel"))
        {
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(_selected));
            Select(null);
            Refresh();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    void Select(AbilityTag tag)
    {
        _selected = tag;
        _so = tag != null ? new SerializedObject(tag) : null;
        Repaint();
    }

    void CreateNew(string tagName)
    {
        EnsureFolder();
        var tag = CreateInstance<AbilityTag>();
        tag.displayName = tagName.Trim().Length > 0 ? tagName.Trim() : "New Tag";
        tag.tagId       = tag.displayName.ToLower().Replace(' ', '_');
        var path = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/ScriptableObjects/AbilityTags/{tag.displayName}.asset");
        AssetDatabase.CreateAsset(tag, path);
        AssetDatabase.SaveAssets();
        Refresh();
        Select(tag);
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/AbilityTags"))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "AbilityTags");
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
