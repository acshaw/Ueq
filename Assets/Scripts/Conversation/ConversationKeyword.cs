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

    [Tooltip("Null = no faction gate")]
    public FactionDefinition RequiredFaction;
    [Tooltip("Minimum standing name required (e.g. Indifferent). Empty = any standing.")]
    public string RequiredStanding;

    [Tooltip("Active keywords unlocked for this player after a successful match")]
    public List<string> UnlocksKeywords = new();
}
