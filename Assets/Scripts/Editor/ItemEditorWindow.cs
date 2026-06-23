using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ItemEditorWindow : EditorWindow
{
    [MenuItem("Tools/Editor/Item Editor")]
    static void Open() => GetWindow<ItemEditorWindow>("Item Editor");

    // ── State ─────────────────────────────────────────────────────────────────

    List<ItemDefinition> _defs     = new();
    ItemDefinition       _selected;
    SerializedObject     _so;
    Vector2              _listScroll;
    Vector2              _editScroll;
    string               _newName  = "New Item";

    static class S
    {
        public static readonly Color Header = new(0.18f, 0.18f, 0.18f);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable() => Refresh();
    void OnFocus()  => Refresh();

    void Refresh()
    {
        _defs.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def  = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
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

    // ── Left panel ────────────────────────────────────────────────────────────

    void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));

        SectionHeader("Item Definitions");

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

    // ── Right panel ───────────────────────────────────────────────────────────

    void DrawEditor()
    {
        EditorGUILayout.BeginVertical();

        if (_selected == null || _so == null)
        {
            EditorGUILayout.HelpBox("Select an item definition to edit.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        _so.Update();

        _editScroll = EditorGUILayout.BeginScrollView(_editScroll);

        DrawSection("Identity",    "itemId", "displayName", "description");
        DrawSection("Stacking",    "maxStackSize");
        DrawSection("Equipment",   "isEquippable", "equipSlot");
        DrawSection("Stat Bonuses","bonusStr", "bonusSta", "bonusAgi", "bonusDex", "bonusInt", "bonusWis", "bonusCha");
        DrawSection("Weapon Stats","weaponBaseDamage", "weaponDelay", "weaponRange", "weaponCategory");
        DrawSection("Economy",     "buyPrice", "sellPrice");

        EditorGUILayout.EndScrollView();

        if (_so.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selected);

        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ping Asset", GUILayout.Height(22)))
            EditorGUIUtility.PingObject(_selected);

        var path = AssetDatabase.GetAssetPath(_selected);
        bool inResources = path.Contains("Resources/Items");
        if (!inResources)
        {
            GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
            if (GUILayout.Button("Move to Resources/Items", GUILayout.Height(22)))
                MoveToResources(_selected);
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        if (!inResources)
            EditorGUILayout.HelpBox("This asset is not under Resources/Items/ — ItemRegistry won't load it at runtime. Click 'Move to Resources/Items' to fix.", MessageType.Warning);

        EditorGUILayout.EndVertical();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void Select(ItemDefinition def)
    {
        _selected = def;
        _so       = def != null ? new SerializedObject(def) : null;
        Repaint();
    }

    void CreateNew(string itemName)
    {
        EnsureResourcesFolder();

        var def = CreateInstance<ItemDefinition>();
        def.displayName = itemName.Trim().Length > 0 ? itemName.Trim() : "New Item";
        def.itemId      = def.displayName.ToLower().Replace(' ', '_');
        var path = AssetDatabase.GenerateUniqueAssetPath($"Assets/Resources/Items/{def.displayName}.asset");
        AssetDatabase.CreateAsset(def, path);
        AssetDatabase.SaveAssets();
        Refresh();
        Select(def);
    }

    void MoveToResources(ItemDefinition def)
    {
        EnsureResourcesFolder();
        string oldPath = AssetDatabase.GetAssetPath(def);
        string newPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/Resources/Items/{def.displayName}.asset");
        string error   = AssetDatabase.MoveAsset(oldPath, newPath);
        if (!string.IsNullOrEmpty(error))
            Debug.LogError($"[ItemEditor] Move failed: {error}");
        else
            Refresh();
    }

    static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Items"))
            AssetDatabase.CreateFolder("Assets/Resources", "Items");
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
