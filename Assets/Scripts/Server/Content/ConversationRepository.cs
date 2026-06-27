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
            "response, required_faction_id, required_standing " +
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

        var rows = new List<ConversationSetSnapshot>(order.Count);
        foreach (var id in order) rows.Add(sets[id]);
        return rows;
    }
}
