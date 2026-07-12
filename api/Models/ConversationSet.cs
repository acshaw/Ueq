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

    // 3.2 quest transaction bundle
    public int RewardXp { get; set; }
    public int RewardCopper { get; set; }
    public int RequiredCopper { get; set; }
    public List<ConversationKeywordRequiredItem> RequiredItems { get; set; } = new();
    public List<ConversationKeywordRewardItem> RewardItems { get; set; } = new();
    public List<ConversationKeywordFactionHit> FactionHits { get; set; } = new();
}

public class ConversationKeywordUnlock
{
    public long Id { get; set; }
    public long KeywordId { get; set; }
    public string UnlockedKeyword { get; set; } = string.Empty;
}

public class ConversationKeywordRequiredItem
{
    public long Id { get; set; }
    public long KeywordId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public class ConversationKeywordRewardItem
{
    public long Id { get; set; }
    public long KeywordId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public class ConversationKeywordFactionHit
{
    public long Id { get; set; }
    public long KeywordId { get; set; }
    public string FactionId { get; set; } = string.Empty;
    public int Delta { get; set; }
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

    // 3.2 quest transaction bundle
    public int RewardXp { get; set; }
    public int RewardCopper { get; set; }
    public int RequiredCopper { get; set; }
    public List<ItemAmountDto> RequiredItems { get; set; } = new();
    public List<ItemAmountDto> RewardItems { get; set; } = new();
    public List<FactionHitDto> FactionHits { get; set; } = new();
}

public class ItemAmountDto
{
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public class FactionHitDto
{
    public string FactionId { get; set; } = string.Empty;
    public int Delta { get; set; }
}
