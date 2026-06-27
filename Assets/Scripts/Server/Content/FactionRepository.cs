using System.Collections.Generic;
using Npgsql;

/// <summary>Plain-data view of one faction (M2.6) — id, display name, and its ally/hostile relations.</summary>
public struct FactionSnapshot
{
    public string                        FactionId;
    public string                        FactionName;
    public List<FactionRelationSnapshot> Relations;
}

/// <summary>One NPC-to-NPC relation: this faction → other, ally or hostile.</summary>
public struct FactionRelationSnapshot
{
    public string OtherFactionId;
    public string Relation;   // "ally" | "hostile"
}

/// <summary>One named standing on the shared ladder.</summary>
public struct FactionThresholdSnapshot
{
    public string Name;
    public int    MinScore;
    public int    SortOrder;
}

/// <summary>One race→faction starting score.</summary>
public struct RaceFactionDefaultSnapshot
{
    public string Race;
    public string FactionId;
    public int    Score;
}

/// <summary>The whole faction content set, loaded together so <see cref="FactionRegistry"/> can wire
/// relations + race defaults against the built faction instances.</summary>
public struct FactionContent
{
    public List<FactionThresholdSnapshot>    Thresholds;
    public List<FactionSnapshot>             Factions;
    public List<RaceFactionDefaultSnapshot>  RaceDefaults;
}

/// <summary>
/// Read-only repository over <c>faction_thresholds</c> / <c>factions</c> / <c>faction_relations</c> /
/// <c>race_faction_defaults</c> (M2.6). Server-only (faction definitions are evaluated on the server;
/// only player score numbers reach clients). 1.2 DAL convention.
/// </summary>
public sealed class FactionRepository : IRepository
{
    public FactionContent LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var content = new FactionContent
        {
            Thresholds   = new List<FactionThresholdSnapshot>(),
            Factions     = new List<FactionSnapshot>(),
            RaceDefaults = new List<RaceFactionDefaultSnapshot>(),
        };

        using (var cmd = new NpgsqlCommand(
            "SELECT name, min_score, sort_order FROM faction_thresholds ORDER BY sort_order, min_score",
            conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                content.Thresholds.Add(new FactionThresholdSnapshot
                {
                    Name      = reader.GetString(0),
                    MinScore  = reader.GetInt32(1),
                    SortOrder = reader.GetInt32(2),
                });
        }

        // Faction headers first, then attach relations, so a faction with no relations still loads.
        var byId = new Dictionary<string, FactionSnapshot>();
        using (var cmd = new NpgsqlCommand(
            "SELECT faction_id, faction_name FROM factions ORDER BY faction_id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetString(0);
                byId[id] = new FactionSnapshot
                {
                    FactionId   = id,
                    FactionName = reader.GetString(1),
                    Relations   = new List<FactionRelationSnapshot>(),
                };
            }
        }

        // Relations share the snapshot's list ref (struct copy + reference-type list), so adding here
        // is visible to the copy placed in content.Factions below.
        using (var cmd = new NpgsqlCommand(
            "SELECT faction_id, other_faction_id, relation FROM faction_relations", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var fid = reader.GetString(0);
                if (byId.TryGetValue(fid, out var snap))
                    snap.Relations.Add(new FactionRelationSnapshot
                    {
                        OtherFactionId = reader.GetString(1),
                        Relation       = reader.GetString(2),
                    });
            }
        }
        content.Factions.AddRange(byId.Values);

        using (var cmd = new NpgsqlCommand(
            "SELECT race, faction_id, score FROM race_faction_defaults", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                content.RaceDefaults.Add(new RaceFactionDefaultSnapshot
                {
                    Race      = reader.GetString(0),
                    FactionId = reader.GetString(1),
                    Score     = reader.GetInt32(2),
                });
        }

        return content;
    }
}
