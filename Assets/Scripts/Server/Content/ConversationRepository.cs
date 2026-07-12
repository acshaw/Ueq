using System.Collections.Generic;
using Npgsql;

/// <summary>Plain-data view of one conversation keyword (M2.4).</summary>
public struct ConversationKeywordSnapshot
{
    public string       Keyword;
    public int          Mode;            // KeywordMode
    public bool         IsOpener;
    public bool         EndsConversation;
    public bool         RequiresUnlock;
    public string       Response;
    public string       RequiredFactionId;
    public string       RequiredStanding;
    public List<string> Unlocks;

    // 3.2 quest transaction bundle
    public int                     RequiredCopper;
    public int                     RewardXp;
    public int                     RewardCopper;
    public List<KeywordItemAmount> RequiredItems;
    public List<KeywordItemAmount> RewardItems;
    public List<KeywordFactionHit> RewardFactionHits;
}

/// <summary>Plain-data view of one conversation set (M2.4) — a named, ordered list of keywords.</summary>
public struct ConversationSetSnapshot
{
    public string                           SetId;
    public string                           DisplayName;
    public List<ConversationKeywordSnapshot> Keywords;
}

/// <summary>
/// Read-only repository over <c>conversation_sets</c> (+ keywords + unlocks), M2.4. Server-only
/// (the conversation state machine never leaves the server). 1.2 DAL convention.
/// </summary>
public sealed class ConversationRepository : IRepository
{
    public List<ConversationSetSnapshot> LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var sets  = new Dictionary<string, ConversationSetSnapshot>();
        var order = new List<string>();
        var kwById = new Dictionary<long, ConversationKeywordSnapshot>();

        using (var cmd = new NpgsqlCommand(
            "SELECT set_id, display_name FROM conversation_sets ORDER BY set_id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetString(0);
                sets[id] = new ConversationSetSnapshot
                {
                    SetId       = id,
                    DisplayName = reader.GetString(1),
                    Keywords    = new List<ConversationKeywordSnapshot>(),
                };
                order.Add(id);
            }
        }

        // Keywords — append to each set in sort order as we read. The snapshot is a struct but its
        // Unlocks is a List (reference type), so the copy added to the set and the copy in kwById share
        // the same list; the unlock pass below mutates both at once.
        using (var cmd = new NpgsqlCommand(
            "SELECT id, set_id, keyword, mode, is_opener, ends_conversation, requires_unlock, " +
            "response, required_faction_id, required_standing, " +
            "reward_xp, reward_copper, required_copper " +
            "FROM conversation_keywords ORDER BY set_id, sort_order, id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                long id  = reader.GetInt64(0);
                var setId = reader.GetString(1);
                var kw = new ConversationKeywordSnapshot
                {
                    Keyword           = reader.GetString(2),
                    Mode              = reader.GetInt32(3),
                    IsOpener          = reader.GetBoolean(4),
                    EndsConversation  = reader.GetBoolean(5),
                    RequiresUnlock    = reader.GetBoolean(6),
                    Response          = reader.GetString(7),
                    RequiredFactionId = reader.IsDBNull(8) ? null : reader.GetString(8),
                    RequiredStanding  = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Unlocks           = new List<string>(),
                    RewardXp          = reader.GetInt32(10),
                    RewardCopper      = reader.GetInt32(11),
                    RequiredCopper    = reader.GetInt32(12),
                    RequiredItems     = new List<KeywordItemAmount>(),
                    RewardItems       = new List<KeywordItemAmount>(),
                    RewardFactionHits = new List<KeywordFactionHit>(),
                };
                kwById[id] = kw;
                if (sets.TryGetValue(setId, out var set)) set.Keywords.Add(kw);
            }
        }

        // Unlocks → attach to their keyword snapshot (shared list, so the set's copy sees it too).
        using (var cmd = new NpgsqlCommand(
            "SELECT keyword_id, unlocked_keyword FROM conversation_keyword_unlocks", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                long kid = reader.GetInt64(0);
                if (kwById.TryGetValue(kid, out var kw))
                    kw.Unlocks.Add(reader.GetString(1));
            }
        }

        // 3.2 transaction bundle — required items, reward items, reward faction hits (shared-list trick).
        LoadItemAmounts(conn, tx, "conversation_keyword_required_items", kwById, k => k.RequiredItems);
        LoadItemAmounts(conn, tx, "conversation_keyword_reward_items",   kwById, k => k.RewardItems);

        using (var cmd = new NpgsqlCommand(
            "SELECT keyword_id, faction_id, delta FROM conversation_keyword_faction_hits", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                long kid = reader.GetInt64(0);
                if (kwById.TryGetValue(kid, out var kw))
                    kw.RewardFactionHits.Add(new KeywordFactionHit
                    {
                        factionId = reader.GetString(1),
                        delta     = reader.GetInt32(2),
                    });
            }
        }

        var rows = new List<ConversationSetSnapshot>(order.Count);
        foreach (var id in order) rows.Add(sets[id]);
        return rows;
    }

    // Load a {keyword_id, item_id, quantity} child table into each keyword's shared item list. The table
    // name is a fixed constant (never user input), so interpolation is safe.
    static void LoadItemAmounts(NpgsqlConnection conn, NpgsqlTransaction tx, string table,
        Dictionary<long, ConversationKeywordSnapshot> kwById,
        System.Func<ConversationKeywordSnapshot, List<KeywordItemAmount>> pick)
    {
        using var cmd = new NpgsqlCommand($"SELECT keyword_id, item_id, quantity FROM {table}", conn, tx);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            long kid = reader.GetInt64(0);
            if (kwById.TryGetValue(kid, out var kw))
                pick(kw).Add(new KeywordItemAmount { itemId = reader.GetString(1), quantity = reader.GetInt32(2) });
        }
    }
}
