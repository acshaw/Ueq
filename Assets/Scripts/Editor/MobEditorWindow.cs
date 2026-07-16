using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MobEditorWindow : EditorWindow
{
    [MenuItem("Tools/Editor/Mob Editor")]
    static void Open() => GetWindow<MobEditorWindow>("Mob Editor");

    // ── State ─────────────────────────────────────────────────────────────────

    List<MobDefinition> _defs     = new();
    MobDefinition       _selected;
    SerializedObject    _so;
    Vector2             _listScroll;
    Vector2             _editScroll;
    string              _newName  = "New Mob";

    static class S
    {
        // section colors
        public static readonly Color Header = new(0.18f, 0.18f, 0.18f);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()  => Refresh();
    void OnFocus()   => Refresh();

    void Refresh()
    {
        _defs.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:MobDefinition"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def  = AssetDatabase.LoadAssetAtPath<MobDefinition>(path);
            if (def != null) _defs.Add(def);
        }
        _defs.Sort((a, b) => string.Compare(a.displayName, b.displayName));
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        DrawList();
        DrawEditor();
        EditorGUILayout.EndHorizontal();
    }

    // ── Left panel: list ──────────────────────────────────────────────────────

    void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));

        SectionHeader("Mob Definitions");

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

    // ── Right panel: editor ───────────────────────────────────────────────────

    void DrawEditor()
    {
        EditorGUILayout.BeginVertical();

        if (_selected == null || _so == null)
        {
            EditorGUILayout.HelpBox("Select a mob definition to edit.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        _so.Update();

        _editScroll = EditorGUILayout.BeginScrollView(_editScroll);

        DrawSection("Identity",  "displayName", "mobLevel", "prefab");
        DrawSection("Combat",    "maxHealth", "attackDamage", "attackInterval", "attackRange");
        DrawMovementSection();
        DrawSection("AI",        "perceptionRadius", "baseAggroThreat");
        DrawSection("Faction",   "faction", "aggroMaxStanding", "warningMaxStanding");
        DrawSection("Combat Pipeline (5.1)", "weaponCategory", "weaponSkill", "combatTable",
                     "attackIsParryable", "avoidanceAgility", "avoidanceDexterity");
        DrawSection("Loot",      "lootTable");
        DrawSection("Rewards",   "xpReward");
        DrawSection("Vendor",       "vendorId", "vendorOpenKeyword");
        DrawSection("Conversation", "conversationSetId");

        EditorGUILayout.EndScrollView();

        if (_so.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selected);

        GUILayout.Space(4);
        if (GUILayout.Button("Ping Asset", GUILayout.Height(22)))
            EditorGUIUtility.PingObject(_selected);

        EditorGUILayout.EndVertical();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void Select(MobDefinition def)
    {
        _selected = def;
        _so       = def != null ? new SerializedObject(def) : null;
        Repaint();
    }

    void CreateNew(string mobName)
    {
        const string dir = "Assets/ScriptableObjects/Mobs";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Mobs");

        var def = CreateInstance<MobDefinition>();
        def.displayName = mobName.Trim().Length > 0 ? mobName.Trim() : "New Mob";
        var path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{def.displayName}.asset");
        AssetDatabase.CreateAsset(def, path);
        AssetDatabase.SaveAssets();
        Refresh();
        Select(def);
    }

    void DrawMovementSection()
    {
        SectionHeader("Movement");
        EditorGUI.indentLevel++;

        EditorGUILayout.PropertyField(_so.FindProperty("movementType"));
        EditorGUILayout.PropertyField(_so.FindProperty("moveSpeed"));

        if (_selected.movementType == MovementType.Wander)
        {
            EditorGUILayout.PropertyField(_so.FindProperty("wanderRadius"));
            EditorGUILayout.PropertyField(_so.FindProperty("wanderPauseMin"));
            EditorGUILayout.PropertyField(_so.FindProperty("wanderPauseMax"));
        }

        EditorGUI.indentLevel--;
        GUILayout.Space(4);
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
        var rect = GUILayoutUtility.GetRect(1, 20, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, S.Header);
        var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };
        style.normal.textColor = Color.white;
        EditorGUI.LabelField(new Rect(rect.x + 6, rect.y, rect.width, rect.height), title, style);
        GUILayout.Space(2);
    }
}
