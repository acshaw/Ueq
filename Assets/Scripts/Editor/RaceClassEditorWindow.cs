using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RaceClassEditorWindow : EditorWindow
{
    [MenuItem("Tools/Editor/Race & Class Editor")]
    static void Open() => GetWindow<RaceClassEditorWindow>("Race & Class Editor");

    // ── State ─────────────────────────────────────────────────────────────────

    enum Tab { Races, Classes, XpTable }
    Tab _tab;

    // Races
    List<RaceDefinition>  _races           = new();
    RaceDefinition        _selectedRace;
    SerializedObject      _raceSO;
    Vector2               _raceListScroll;
    Vector2               _raceEditScroll;
    string                _newRaceName     = "New Race";

    // Classes
    List<ClassDefinition> _classes         = new();
    ClassDefinition       _selectedClass;
    SerializedObject      _classSO;
    Vector2               _classListScroll;
    Vector2               _classEditScroll;
    string                _newClassName    = "New Class";

    // XP Table
    XpTableDefinition     _xpTable;
    SerializedObject      _xpTableSO;
    Vector2               _xpTableScroll;

    static class S
    {
        public static readonly Color Header      = new(0.18f, 0.18f, 0.18f);
        public static readonly Color TabActive   = new(0.25f, 0.45f, 0.70f);
        public static readonly Color TabInactive = new(0.22f, 0.22f, 0.22f);
        public static readonly Color RowEven     = new(0.20f, 0.20f, 0.20f);
        public static readonly Color RowOdd      = new(0.23f, 0.23f, 0.23f);
    }

    const string XpTableResourcePath = "Assets/Resources/XpTable.asset";
    const string XpTableLoadPath     = "XpTable"; // Resources.Load key

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable() => Refresh();
    void OnFocus()  => Refresh();

    void Refresh()
    {
        _races.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:RaceDefinition"))
        {
            var def = AssetDatabase.LoadAssetAtPath<RaceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def != null) _races.Add(def);
        }
        _races.Sort((a, b) => string.Compare(a.raceName, b.raceName));

        _classes.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:ClassDefinition"))
        {
            var def = AssetDatabase.LoadAssetAtPath<ClassDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def != null) _classes.Add(def);
        }
        _classes.Sort((a, b) => string.Compare(a.className, b.className));

        _xpTable   = AssetDatabase.LoadAssetAtPath<XpTableDefinition>(XpTableResourcePath);
        _xpTableSO = _xpTable != null ? new SerializedObject(_xpTable) : null;
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        DrawTabs();
        if      (_tab == Tab.Races)    { EditorGUILayout.BeginHorizontal(); DrawRaceList();  DrawRaceEditor();  EditorGUILayout.EndHorizontal(); }
        else if (_tab == Tab.Classes)  { EditorGUILayout.BeginHorizontal(); DrawClassList(); DrawClassEditor(); EditorGUILayout.EndHorizontal(); }
        else                           { DrawXpTableEditor(); }
    }

    void DrawTabs()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        DrawTab(Tab.Races,    "Races");
        DrawTab(Tab.Classes,  "Classes");
        DrawTab(Tab.XpTable,  "XP Table");
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    void DrawTab(Tab t, string label)
    {
        GUI.backgroundColor = _tab == t ? S.TabActive : S.TabInactive;
        if (GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.Width(80)))
            _tab = t;
        GUI.backgroundColor = Color.white;
    }

    // ── Race list ─────────────────────────────────────────────────────────────

    void DrawRaceList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        SectionHeader("Race Definitions");

        _raceListScroll = EditorGUILayout.BeginScrollView(_raceListScroll);
        foreach (var def in _races)
        {
            bool active = def == _selectedRace;
            GUI.backgroundColor = active ? Color.cyan : Color.white;
            if (GUILayout.Button(def.raceName, EditorStyles.toolbarButton))
                SelectRace(def);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        GUILayout.Space(4);
        _newRaceName = EditorGUILayout.TextField(_newRaceName);
        if (GUILayout.Button("Create New", GUILayout.Height(28)))
            CreateNewRace(_newRaceName);

        EditorGUILayout.EndVertical();
    }

    void DrawRaceEditor()
    {
        EditorGUILayout.BeginVertical();

        if (_selectedRace == null || _raceSO == null)
        {
            EditorGUILayout.HelpBox("Select a race to edit.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        _raceSO.Update();
        _raceEditScroll = EditorGUILayout.BeginScrollView(_raceEditScroll);

        SectionHeader("Identity");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_raceSO.FindProperty("raceName"));
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        SectionHeader("XP");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_raceSO.FindProperty("xpModifier"));
        DrawModifierHelp(_selectedRace.xpModifier);
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        SectionHeader("Stat Modifiers");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_raceSO.FindProperty("strMod"), new GUIContent("STR"));
        EditorGUILayout.PropertyField(_raceSO.FindProperty("staMod"), new GUIContent("STA"));
        EditorGUILayout.PropertyField(_raceSO.FindProperty("agiMod"), new GUIContent("AGI"));
        EditorGUILayout.PropertyField(_raceSO.FindProperty("dexMod"), new GUIContent("DEX"));
        EditorGUILayout.PropertyField(_raceSO.FindProperty("intMod"), new GUIContent("INT"));
        EditorGUILayout.PropertyField(_raceSO.FindProperty("wisMod"), new GUIContent("WIS"));
        EditorGUILayout.PropertyField(_raceSO.FindProperty("chaMod"), new GUIContent("CHA"));
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        EditorGUILayout.EndScrollView();

        if (_raceSO.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selectedRace);

        GUILayout.Space(4);
        if (GUILayout.Button("Ping Asset", GUILayout.Height(22)))
            EditorGUIUtility.PingObject(_selectedRace);

        EditorGUILayout.EndVertical();
    }

    void SelectRace(RaceDefinition def)
    {
        _selectedRace = def;
        _raceSO = def != null ? new SerializedObject(def) : null;
        Repaint();
    }

    void CreateNewRace(string name)
    {
        EnsureFolder("Assets/Resources", "Races");
        var def = CreateInstance<RaceDefinition>();
        def.raceName = name.Trim().Length > 0 ? name.Trim() : "New Race";
        var path = AssetDatabase.GenerateUniqueAssetPath($"Assets/Resources/Races/{def.raceName}.asset");
        AssetDatabase.CreateAsset(def, path);
        AssetDatabase.SaveAssets();
        Refresh();
        SelectRace(def);
    }

    // ── Class list ────────────────────────────────────────────────────────────

    void DrawClassList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        SectionHeader("Class Definitions");

        _classListScroll = EditorGUILayout.BeginScrollView(_classListScroll);
        foreach (var def in _classes)
        {
            bool active = def == _selectedClass;
            GUI.backgroundColor = active ? Color.cyan : Color.white;
            if (GUILayout.Button(def.className, EditorStyles.toolbarButton))
                SelectClass(def);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        GUILayout.Space(4);
        _newClassName = EditorGUILayout.TextField(_newClassName);
        if (GUILayout.Button("Create New", GUILayout.Height(28)))
            CreateNewClass(_newClassName);

        EditorGUILayout.EndVertical();
    }

    void DrawClassEditor()
    {
        EditorGUILayout.BeginVertical();

        if (_selectedClass == null || _classSO == null)
        {
            EditorGUILayout.HelpBox("Select a class to edit.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        _classSO.Update();
        _classEditScroll = EditorGUILayout.BeginScrollView(_classEditScroll);

        SectionHeader("Identity");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_classSO.FindProperty("className"));
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        SectionHeader("XP");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_classSO.FindProperty("xpModifier"));
        DrawModifierHelp(_selectedClass.xpModifier);
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        SectionHeader("Base Stats");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_classSO.FindProperty("baseStr"), new GUIContent("STR"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("baseSta"), new GUIContent("STA"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("baseAgi"), new GUIContent("AGI"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("baseDex"), new GUIContent("DEX"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("baseInt"), new GUIContent("INT"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("baseWis"), new GUIContent("WIS"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("baseCha"), new GUIContent("CHA"));
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        SectionHeader("HP Formula");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_classSO.FindProperty("classBaseHP"),   new GUIContent("Base HP"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("hpPerLevel"),    new GUIContent("HP per Level"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("staCap"),        new GUIContent("STA Cap"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("baseStaRatio"),  new GUIContent("Base STA Ratio"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("staGrowthRate"), new GUIContent("STA Growth Rate"));
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        SectionHeader("Mana Formula");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_classSO.FindProperty("manaStatType"),   new GUIContent("Mana Stat"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("classBaseMana"),  new GUIContent("Base Mana"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("manaPerLevel"),   new GUIContent("Mana per Level"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("manaCap"),        new GUIContent("Mana Cap"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("baseManaRatio"),  new GUIContent("Base Mana Ratio"));
        EditorGUILayout.PropertyField(_classSO.FindProperty("manaGrowthRate"), new GUIContent("Mana Growth Rate"));
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        SectionHeader("Known Abilities");
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_classSO.FindProperty("startingAbilities"),
            new GUIContent("Starting Abilities"), true);
        EditorGUI.indentLevel--;
        GUILayout.Space(4);

        EditorGUILayout.EndScrollView();

        if (_classSO.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selectedClass);

        GUILayout.Space(4);
        if (GUILayout.Button("Ping Asset", GUILayout.Height(22)))
            EditorGUIUtility.PingObject(_selectedClass);

        EditorGUILayout.EndVertical();
    }

    void SelectClass(ClassDefinition def)
    {
        _selectedClass = def;
        _classSO = def != null ? new SerializedObject(def) : null;
        Repaint();
    }

    void CreateNewClass(string name)
    {
        EnsureFolder("Assets/Resources", "Classes");
        var def = CreateInstance<ClassDefinition>();
        def.className = name.Trim().Length > 0 ? name.Trim() : "New Class";
        var path = AssetDatabase.GenerateUniqueAssetPath($"Assets/Resources/Classes/{def.className}.asset");
        AssetDatabase.CreateAsset(def, path);
        AssetDatabase.SaveAssets();
        Refresh();
        SelectClass(def);
    }

    // ── XP Table editor ───────────────────────────────────────────────────────

    void DrawXpTableEditor()
    {
        GUILayout.Space(4);

        if (_xpTable == null)
        {
            EditorGUILayout.HelpBox(
                "No XP Table asset found at Assets/Resources/XpTable.asset.\n" +
                "Create one to enable in-editor editing of level XP costs.",
                MessageType.Warning);
            GUILayout.Space(8);
            if (GUILayout.Button("Create Default XP Table", GUILayout.Height(32)))
                CreateDefaultXpTable();
            return;
        }

        // Toolbar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label($"XP Table  ({_xpTable.Count} levels)", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset to Defaults", EditorStyles.toolbarButton, GUILayout.Width(120)))
        {
            if (EditorUtility.DisplayDialog("Reset XP Table",
                    "Reset all values to the original defaults?", "Reset", "Cancel"))
            {
                Undo.RecordObject(_xpTable, "Reset XP Table");
                _xpTable.xpPerLevel = (int[])XpTableDefinition.DefaultValues.Clone();
                EditorUtility.SetDirty(_xpTable);
                AssetDatabase.SaveAssets();
                _xpTableSO = new SerializedObject(_xpTable);
            }
        }
        if (GUILayout.Button("Ping Asset", EditorStyles.toolbarButton, GUILayout.Width(80)))
            EditorGUIUtility.PingObject(_xpTable);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // Column headers
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Level", EditorStyles.miniLabel,  GUILayout.Width(44));
        GUILayout.Label("XP to complete level", EditorStyles.miniLabel, GUILayout.Width(160));
        GUILayout.Label("Cumulative XP to reach level", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        _xpTableSO.Update();
        var arrayProp = _xpTableSO.FindProperty("xpPerLevel");

        _xpTableScroll = EditorGUILayout.BeginScrollView(_xpTableScroll);

        int cumulative = 0;
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            var rowColor = i % 2 == 0 ? S.RowEven : S.RowOdd;
            var rowRect  = EditorGUILayout.BeginHorizontal();
            EditorGUI.DrawRect(rowRect, rowColor);

            GUILayout.Label($"{i + 1}", GUILayout.Width(44));

            var elem = arrayProp.GetArrayElementAtIndex(i);
            elem.intValue = EditorGUILayout.IntField(elem.intValue, GUILayout.Width(160));

            int reachNext = cumulative + Mathf.Max(0, elem.intValue);
            GUILayout.Label($"{reachNext:N0}", EditorStyles.miniLabel);

            cumulative = reachNext;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        if (_xpTableSO.ApplyModifiedProperties())
            EditorUtility.SetDirty(_xpTable);
    }

    void CreateDefaultXpTable()
    {
        EnsureFolder("Assets", "Resources");
        var asset = CreateInstance<XpTableDefinition>();
        asset.xpPerLevel = (int[])XpTableDefinition.DefaultValues.Clone();
        AssetDatabase.CreateAsset(asset, XpTableResourcePath);
        AssetDatabase.SaveAssets();
        Refresh();
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    static void DrawModifierHelp(float modifier)
    {
        string label;
        if (Mathf.Approximately(modifier, 1f)) label = "Standard XP rate";
        else if (modifier < 1f)                label = $"{Mathf.RoundToInt((1f - modifier) * 100f)}% less XP required";
        else                                   label = $"{Mathf.RoundToInt((modifier - 1f) * 100f)}% more XP required";
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
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

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }
}
