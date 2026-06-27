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
}
