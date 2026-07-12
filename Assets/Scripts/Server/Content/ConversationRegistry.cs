using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Server-only lookup of conversation keyword sets by id (M2.4). Builds a runtime
/// <see cref="ConversationKeywordSet"/> per DB row (the same type the SO path used), so
/// <c>NpcConversation</c>'s state machine is unchanged — only the source moved off ScriptableObjects.
/// No client sync (the whole conversation flow is server-side). Populated by <c>ContentLoader</c>.
/// </summary>
public static class ConversationRegistry
{
    static readonly Dictionary<string, ConversationKeywordSet> _byId = new();

    public static void LoadFrom(IEnumerable<ConversationSetSnapshot> snapshots)
    {
        _byId.Clear();
        foreach (var s in snapshots)
            if (!string.IsNullOrEmpty(s.SetId))
                _byId[s.SetId] = Build(s);
    }

    public static ConversationKeywordSet Get(string setId)
        => string.IsNullOrEmpty(setId) ? null : _byId.GetValueOrDefault(setId);

    static ConversationKeywordSet Build(ConversationSetSnapshot s)
    {
        var set = ScriptableObject.CreateInstance<ConversationKeywordSet>();
        set.name = s.SetId;
        set.Keywords = new List<ConversationKeyword>(s.Keywords.Count);
        foreach (var k in s.Keywords)
        {
            set.Keywords.Add(new ConversationKeyword
            {
                Keyword              = k.Keyword,
                Mode                 = (KeywordMode)k.Mode,
                IsConversationOpener = k.IsOpener,
                EndsConversation     = k.EndsConversation,
                RequiresUnlock       = k.RequiresUnlock,
                Response             = k.Response,
                RequiredFaction      = null,               // DB sets resolve the gate by id (2.6)
                RequiredFactionId    = k.RequiredFactionId,
                RequiredStanding     = k.RequiredStanding,
                UnlocksKeywords      = k.Unlocks ?? new List<string>(),
                RequiredCopper       = k.RequiredCopper,
                RequiredItems        = k.RequiredItems     ?? new List<KeywordItemAmount>(),
                RewardXp             = k.RewardXp,
                RewardCopper         = k.RewardCopper,
                RewardItems          = k.RewardItems       ?? new List<KeywordItemAmount>(),
                RewardFactionHits    = k.RewardFactionHits ?? new List<KeywordFactionHit>(),
            });
        }
        return set;
    }
}
