using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Conversation/Keyword Set")]
public class ConversationKeywordSet : ScriptableObject
{
    public List<ConversationKeyword> Keywords = new();
}
