namespace Ueq.ContentApi.Models;

/// <summary>EF entities for conversation sets (M2.4). Mapping-only; SQL runner owns the schema.</summary>
public class ConversationSet
{
    public string SetId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public List<ConversationKeywordRow> Keywords { get; set; } = new();
}

public class ConversationKeywordRow
{
    public long Id { get; set; }
    public string SetId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public int Mode { get; set; }
    public bool IsOpener { get; set; }
    public bool EndsConversation { get; set; }
    public bool RequiresUnlock { get; set; }
    public string Response { get; set; } = string.Empty;
    public string? RequiredFactionId { get; set; }
    public string? RequiredStanding { get; set; }
    public List<ConversationKeywordUnlock> Unlocks { get; set; } = new();
}

public class ConversationKeywordUnlock
{
    public long Id { get; set; }
    public long KeywordId { get; set; }
    public string UnlockedKeyword { get; set; } = string.Empty;
}

// ── Flat DTOs the Angular editor works with ────────────────────────────────────────────────
public class ConversationSetDto
{
    public string SetId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<ConversationKeywordDto> Keywords { get; set; } = new();
}

public class ConversationKeywordDto
{
    public string Keyword { get; set; } = string.Empty;
    public int Mode { get; set; }
    public bool IsOpener { get; set; }
    public bool EndsConversation { get; set; }
    public bool RequiresUnlock { get; set; }
    public string Response { get; set; } = string.Empty;
    public string? RequiredFactionId { get; set; }
    public string? RequiredStanding { get; set; }
    public List<string> Unlocks { get; set; } = new();
}
