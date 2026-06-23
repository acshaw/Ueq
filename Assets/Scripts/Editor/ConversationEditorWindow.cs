using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ConversationEditorWindow : EditorWindow
{
    [MenuItem("Tools/Editor/Conversation Editor")]
    static void Open() => GetWindow<ConversationEditorWindow>("Conversation Editor");

    public static void OpenWith(ConversationKeywordSet set)
    {
        var window = GetWindow<ConversationEditorWindow>("Conversation Editor");
        window.Select(set);
        window.Focus();
    }

    // ── State ─────────────────────────────────────────────────────────────────

    List<ConversationKeywordSet> _sets     = new();
    ConversationKeywordSet       _selected;
    SerializedObject             _so;
    Vector2                      _listScroll;
    Vector2                      _editScroll;
    string                       _newName  = "New Keyword Set";
    readonly List<bool>          _foldouts = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable() => Refresh();
    void OnFocus()  => Refresh();

    void Refresh()
    {
        _sets.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:ConversationKeywordSet"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var set  = AssetDatabase.LoadAssetAtPath<ConversationKeywordSet>(path);
            if (set != null) _sets.Add(set);
        }
        _sets.Sort((a, b) => string.Compare(a.name, b.name));
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
        SectionHeader("Keyword Sets");

        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
        foreach (var set in _sets)
        {
            bool active = set == _selected;
            GUI.backgroundColor = active ? Color.cyan : Color.white;
            if (GUILayout.Button(set.name, EditorStyles.toolbarButton))
                Select(set);
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
            EditorGUILayout.HelpBox("Select a keyword set to edit.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        _so.Update();
        _editScroll = EditorGUILayout.BeginScrollView(_editScroll);

        var keywordsProp = _so.FindProperty("Keywords");

        while (_foldouts.Count < keywordsProp.arraySize) _foldouts.Add(true);
        while (_foldouts.Count > keywordsProp.arraySize) _foldouts.RemoveAt(_foldouts.Count - 1);

        SectionHeader($"Keywords ({keywordsProp.arraySize})");

        int toDelete = -1;

        for (int i = 0; i < keywordsProp.arraySize; i++)
        {
            var    kwProp    = keywordsProp.GetArrayElementAtIndex(i);
            var    nameProp  = kwProp.FindPropertyRelative("Keyword");
            string label     = string.IsNullOrEmpty(nameProp.stringValue) ? $"[{i}]" : nameProp.stringValue;

            EditorGUILayout.BeginHorizontal();
            _foldouts[i] = EditorGUILayout.Foldout(_foldouts[i], label, true, EditorStyles.foldoutHeader);
            if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18))) toDelete = i;
            EditorGUILayout.EndHorizontal();

            if (_foldouts[i])
            {
                EditorGUI.indentLevel++;
                DrawKeyword(kwProp);
                EditorGUI.indentLevel--;
                GUILayout.Space(4);
            }
        }

        if (toDelete >= 0)
        {
            keywordsProp.DeleteArrayElementAtIndex(toDelete);
            _foldouts.RemoveAt(toDelete);
        }

        GUILayout.Space(6);
        if (GUILayout.Button("+ Add Keyword", GUILayout.Height(26)))
        {
            keywordsProp.InsertArrayElementAtIndex(keywordsProp.arraySize);
            _foldouts.Add(true);
        }

        EditorGUILayout.EndScrollView();

        if (_so.ApplyModifiedProperties())
            EditorUtility.SetDirty(_selected);

        GUILayout.Space(4);
        if (GUILayout.Button("Ping Asset", GUILayout.Height(22)))
            EditorGUIUtility.PingObject(_selected);

        EditorGUILayout.EndVertical();
    }

    void DrawKeyword(SerializedProperty kw)
    {
        EditorGUILayout.PropertyField(kw.FindPropertyRelative("Keyword"));
        EditorGUILayout.PropertyField(kw.FindPropertyRelative("Mode"));

        GUILayout.Space(2);
        EditorGUILayout.PropertyField(kw.FindPropertyRelative("IsConversationOpener"));
        EditorGUILayout.PropertyField(kw.FindPropertyRelative("EndsConversation"));
        EditorGUILayout.PropertyField(kw.FindPropertyRelative("RequiresUnlock"));

        GUILayout.Space(2);
        EditorGUILayout.PropertyField(kw.FindPropertyRelative("Response"));

        GUILayout.Space(2);
        EditorGUILayout.PropertyField(kw.FindPropertyRelative("RequiredFaction"));
        EditorGUILayout.PropertyField(kw.FindPropertyRelative("RequiredStanding"));

        GUILayout.Space(2);
        EditorGUILayout.PropertyField(kw.FindPropertyRelative("UnlocksKeywords"), true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void Select(ConversationKeywordSet set)
    {
        _selected = set;
        _so       = set != null ? new SerializedObject(set) : null;
        _foldouts.Clear();
        Repaint();
    }

    void CreateNew(string setName)
    {
        const string dir = "Assets/ScriptableObjects/Conversations";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Conversations");

        var set  = CreateInstance<ConversationKeywordSet>();
        var safe = setName.Trim().Length > 0 ? setName.Trim() : "New Keyword Set";
        var path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{safe}.asset");
        AssetDatabase.CreateAsset(set, path);
        AssetDatabase.SaveAssets();
        Refresh();
        Select(set);
    }

    static void SectionHeader(string title)
    {
        var rect = GUILayoutUtility.GetRect(1, 20, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
        var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };
        style.normal.textColor = Color.white;
        EditorGUI.LabelField(new Rect(rect.x + 6, rect.y, rect.width, rect.height), title, style);
        GUILayout.Space(2);
    }
}
