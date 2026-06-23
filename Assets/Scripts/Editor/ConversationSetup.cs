using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools/Setup Conversation Data — run after Tools/Setup Player Scene and Tools/Setup Faction Data.
public static class ConversationSetup
{
    const string SoPath = "Assets/ScriptableObjects/Conversation";

    [MenuItem("Tools/Setup Conversation Data")]
    public static void Run()
    {
        EnsureDirectory(SoPath);

        var faction  = AssetDatabase.LoadAssetAtPath<FactionDefinition>("Assets/ScriptableObjects/Faction/QeynosGuards.asset");
        var keywords = GetOrCreateKeywordSet(faction);

        WireSceneEnemy(keywords);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ConversationSetup] Done. Assets at {SoPath}/");
    }

    static ConversationKeywordSet GetOrCreateKeywordSet(FactionDefinition faction)
    {
        var path     = $"{SoPath}/GuardKeywords.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ConversationKeywordSet>(path);
        if (existing != null) return existing;

        var set = ScriptableObject.CreateInstance<ConversationKeywordSet>();
        set.Keywords = new List<ConversationKeyword>
        {
            // Passive — hails from any player open a conversation
            new()
            {
                Keyword              = "hail",
                Mode                 = KeywordMode.Passive,
                IsConversationOpener = true,
                Response             = "Hail, <name>! Well met. Ask me about [guards] or [patrol].",
            },
            // Passive — no conversation required; works like a password or ambient trigger
            new()
            {
                Keyword   = "guards",
                Mode      = KeywordMode.Passive,
                Response  = "The guards are stationed at the north gate.",
            },
            // Active — only fires once in conversation
            new()
            {
                Keyword          = "patrol",
                Mode             = KeywordMode.Active,
                Response         = "We patrol these roads to keep travelers like you safe, <race>.",
                UnlocksKeywords  = new List<string> { "danger" },
            },
            // Active — requires unlock from [patrol] match
            new()
            {
                Keyword        = "danger",
                Mode           = KeywordMode.Active,
                RequiresUnlock = true,
                Response       = "Strange creatures have been seen to the east. Be wary.",
            },
            // Active, faction-gated — only responds to Indifferent or better
            new()
            {
                Keyword          = "help",
                Mode             = KeywordMode.Active,
                Response         = "What do you need, adventurer?",
                RequiredFaction  = faction,
                RequiredStanding = "Indifferent",
            },
            // Passive — ends the conversation from either side
            new()
            {
                Keyword          = "farewell",
                Mode             = KeywordMode.Passive,
                EndsConversation = true,
                Response         = "Safe travels, <name>.",
            },
        };

        AssetDatabase.CreateAsset(set, path);
        return set;
    }

    static void WireSceneEnemy(ConversationKeywordSet keywords)
    {
        var enemy = GameObject.Find("Enemy");
        if (enemy == null)
        {
            Debug.LogWarning("[ConversationSetup] No Enemy in scene — run Tools/Setup Player Scene first.");
            return;
        }

        var conv = enemy.GetComponent<NpcConversation>() ?? enemy.AddComponent<NpcConversation>();
        var so   = new SerializedObject(conv);
        so.FindProperty("keywordSet").objectReferenceValue = keywords;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(enemy);
    }

    static void EnsureDirectory(string path)
    {
        var parts   = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
