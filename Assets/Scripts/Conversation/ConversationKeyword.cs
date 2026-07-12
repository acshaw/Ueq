using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConversationKeyword
{
    public string      Keyword;
    public KeywordMode Mode;

    [Tooltip("Triggers OnConversationStart when matched while NPC is Idle")]
    public bool IsConversationOpener;
    [Tooltip("Fires OnConversationEnd and applies cooldown after delivering the response")]
    public bool EndsConversation;
    [Tooltip("Only fires if explicitly unlocked by a prior keyword match this session")]
    public bool RequiresUnlock;

    [TextArea(1, 4)]
    public string Response; // supports <name> <race> <class> <gender>

    [Tooltip("Null = no faction gate (legacy SO ref; DB-backed sets use RequiredFactionId instead)")]
    public FactionDefinition RequiredFaction;
    [Tooltip("M2.4: faction gate by id (resolved via FactionRegistry; live once factions are in DB at 2.6)")]
    public string RequiredFactionId;
    [Tooltip("Minimum standing name required (e.g. Indifferent). Empty = any standing.")]
    public string RequiredStanding;

    [Tooltip("Active keywords unlocked for this player after a successful match")]
    public List<string> UnlocksKeywords = new();

    // ── 3.2: quest transaction bundle ─────────────────────────────────────────
    // Saying this keyword (once its faction gate passes) runs a turn-in: the NPC accepts the required
    // items/coin and grants the reward. All-or-nothing + repeatable (no completion tracking — Q2). Empty
    // bundle = a normal talk-only keyword. Applied by KeywordRewardApplicator.
    [Header("Quest transaction (3.2)")]
    public int RequiredCopper;
    public List<KeywordItemAmount> RequiredItems = new();

    public int RewardXp;
    public int RewardCopper;
    public List<KeywordItemAmount> RewardItems       = new();
    public List<KeywordFactionHit> RewardFactionHits = new();

    public bool HasTransaction =>
        RewardXp > 0 || RewardCopper > 0 || RequiredCopper > 0 ||
        (RewardItems != null && RewardItems.Count > 0) ||
        (RequiredItems != null && RequiredItems.Count > 0) ||
        (RewardFactionHits != null && RewardFactionHits.Count > 0);
}

[System.Serializable]
public struct KeywordItemAmount { public string itemId; public int quantity; }

[System.Serializable]
public struct KeywordFactionHit { public string factionId; public int delta; }
