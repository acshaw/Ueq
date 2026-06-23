using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AbilityEditorWindow : EditorWindow
{
    [MenuItem("Tools/Editor/Ability Editor")]
    static void Open() => GetWindow<AbilityEditorWindow>("Ability Editor");

    List<AbilityDefinition> _defs = new();
    AbilityDefinition        _selected;
    SerializedObject         _so;
    Vector2                  _listScroll;
    Vector2                  _editScroll;
    string                   _newName = "New Ability";

    static class S { public static readonly Color Header = new(0.18f, 0.18f, 0.18f); }

    void OnEnable() => Refresh();
    void OnFocus()  => Refresh();

    void Refresh()
    {
        _defs.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:AbilityDefinition"))
        {
            var def = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def != null) _defs.Add(def);
        }
        _defs.Sort((a, b) => string.Compare(a.displayName, b.displayName));
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
        SectionHeader("Ability Definitions");

        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
        foreach (var def in _defs)
        {
            bool active = def == _selected;
            GUI.backgroundColor = active ? Color.cyan : Color.white;
            if (GUILayout.Button(def.displayName, EditorStyles.toolbarButton))
                Select(def);
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
            EditorGUILayout.HelpBox("Select an ability to edit.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        _so.Update();
        _editScroll = EditorGUILayout.BeginScrollView(_editScroll);

        // Identity
        SectionHeader("Identity");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_so.FindProperty("abilityId"),   new GUIContent("Ability ID"));
        EditorGUILayout.PropertyField(_so.FindProperty("displayName"), new GUIContent("Display Name"));
        EditorGUILayout.PropertyField(_so.FindProperty("description"), new GUIContent("Description"));
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        // Targeting
        SectionHeader("Targeting");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_so.FindProperty("targetingType"), new GUIContent("Targeting"));
        EditorGUILayout.PropertyField(_so.FindProperty("range"),         new GUIContent("Range"));
        EditorGUILayout.PropertyField(_so.FindProperty("castTime"),      new GUIContent("Cast Time (0=instant)"));
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        // Resource
        SectionHeader("Resource");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_so.FindProperty("manaCost"), new GUIContent("Mana Cost (0=free)"));
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        // Animation
        SectionHeader("Animation");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_so.FindProperty("animTrigger"),
            new GUIContent("Anim Trigger", "Animator trigger fired on a successful cast " +
                "(empty = none). Must match a Trigger param + state in PlayerLocomotion.controller."));
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        // Tags
        SectionHeader("Tags");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_so.FindProperty("tags"), new GUIContent("Tags"), true);
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        // Cooldown links
        SectionHeader("Cooldown Links  (empty = uses GCD)");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_so.FindProperty("cooldownLinks"), new GUIContent("Cooldown Links"), true);
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        // Effects
        SectionHeader("Effects  (applied in order)");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_so.FindProperty("effects"), new GUIContent("Effects"), true);
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        EditorGUILayout.EndScrollView();

        if (_so.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selected);

        GUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ping Asset", GUILayout.Height(22)))
            EditorGUIUtility.PingObject(_selected);

        var path = AssetDatabase.GetAssetPath(_selected);
        bool inResources = path.Contains("Resources/Abilities");
        if (!inResources)
        {
            GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
            if (GUILayout.Button("Move to Resources/Abilities", GUILayout.Height(22)))
                MoveToResources(_selected);
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        if (!inResources)
            EditorGUILayout.HelpBox(
                "This asset is not under Resources/Abilities/ — AbilityRegistry won't load it at runtime.",
                MessageType.Warning);

        EditorGUILayout.EndVertical();
    }

    void Select(AbilityDefinition def)
    {
        _selected = def;
        _so = def != null ? new SerializedObject(def) : null;
        Repaint();
    }

    void CreateNew(string abilityName)
    {
        EnsureResourcesFolder();
        var def = CreateInstance<AbilityDefinition>();
        def.displayName = abilityName.Trim().Length > 0 ? abilityName.Trim() : "New Ability";
        def.abilityId   = def.displayName.ToLower().Replace(' ', '_');
        var path = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/Resources/Abilities/{def.displayName}.asset");
        AssetDatabase.CreateAsset(def, path);
        AssetDatabase.SaveAssets();
        Refresh();
        Select(def);
    }

    void MoveToResources(AbilityDefinition def)
    {
        EnsureResourcesFolder();
        string oldPath = AssetDatabase.GetAssetPath(def);
        string newPath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/Resources/Abilities/{def.displayName}.asset");
        string error = AssetDatabase.MoveAsset(oldPath, newPath);
        if (!string.IsNullOrEmpty(error))
            Debug.LogError($"[AbilityEditor] Move failed: {error}");
        else
            Refresh();
    }

    static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Abilities"))
            AssetDatabase.CreateFolder("Assets/Resources", "Abilities");
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
