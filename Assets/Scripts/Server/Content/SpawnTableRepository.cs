using System.Collections.Generic;
using Npgsql;

/// <summary>Plain-data view of one spawn table (M2.7.2) — an inlined timer + weighted mob entries.</summary>
public struct SpawnTableSnapshot
{
    public string                     SpawnTableId;
    public string                     DisplayName;
    public float                      TimerBaseSeconds;
    public float                      TimerVariance;
    public List<SpawnEntrySnapshot>   Entries;
}

public struct SpawnEntrySnapshot
{
    public string MobId;
    public int    Weight;
    public int    GroupSize;
}

/// <summary>
/// Read-only repository over <c>spawn_tables</c> (+ entries), M2.7.2. Server-only (spawning is
/// server-side). Header-then-children load (1.2 DAL convention); the entries list is a reference type
/// shared with the snapshot copy placed in the result.
/// </summary>
public sealed class SpawnTableRepository : IRepository
{
    public List<SpawnTableSnapshot> LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var byId  = new Dictionary<string, SpawnTableSnapshot>();
        var order = new List<string>();

        using (var cmd = new NpgsqlCommand(
            "SELECT spawn_table_id, display_name, timer_base_seconds, timer_variance " +
            "FROM spawn_tables ORDER BY spawn_table_id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetString(0);
                byId[id] = new SpawnTableSnapshot
                {
                    SpawnTableId     = id,
                    DisplayName      = reader.GetString(1),
                    TimerBaseSeconds = reader.GetFloat(2),
                    TimerVariance    = reader.GetFloat(3),
                    Entries          = new List<SpawnEntrySnapshot>(),
                };
                order.Add(id);
            }
        }

        using (var cmd = new NpgsqlCommand(
            "SELECT spawn_table_id, mob_id, weight, group_size FROM spawn_table_entries " +
            "ORDER BY spawn_table_id, sort_order, id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                if (byId.TryGetValue(reader.GetString(0), out var s))
                    s.Entries.Add(new SpawnEntrySnapshot
                    {
                        MobId     = reader.GetString(1),
                        Weight    = reader.GetInt32(2),
                        GroupSize = reader.GetInt32(3),
                    });
        }

        var rows = new List<SpawnTableSnapshot>(order.Count);
        foreach (var id in order) rows.Add(byId[id]);
        return rows;
    }
}
