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
    public string TimeCondition;      // 8.1.4 — Any | DayOnly | NightOnly
    public string LunarCondition;     // 8.1.4 — Any | FullMoonOnly | NewMoonOnly
    public int?   DayOfWeekCondition; // 8.1.4 — 1-7, null = Any
    public int?   MonthCondition;     // 8.1.4 — 1-13, null = Any
    public float? RespawnBaseSeconds; // 8.2 (NR1) — null = use the table's default timer
    public float? RespawnVariance;    // 8.2 (NR1) — null = use the table's default timer
    public int?   MinLevelOverride;   // 8.3 (LV3) — null = no variance (mob's authored level)
    public int?   MaxLevelOverride;   // 8.3 (LV3) — null = no variance (mob's authored level)
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
            "SELECT spawn_table_id, mob_id, weight, group_size, time_condition, lunar_condition, " +
            "day_of_week_condition, month_condition, respawn_base_seconds, respawn_variance, " +
            "min_level_override, max_level_override " +
            "FROM spawn_table_entries ORDER BY spawn_table_id, sort_order, id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                if (byId.TryGetValue(reader.GetString(0), out var s))
                    s.Entries.Add(new SpawnEntrySnapshot
                    {
                        MobId              = reader.GetString(1),
                        Weight             = reader.GetInt32(2),
                        GroupSize          = reader.GetInt32(3),
                        TimeCondition      = reader.GetString(4),
                        LunarCondition     = reader.GetString(5),
                        DayOfWeekCondition = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                        MonthCondition     = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                        RespawnBaseSeconds = reader.IsDBNull(8) ? (float?)null : reader.GetFloat(8),
                        RespawnVariance    = reader.IsDBNull(9) ? (float?)null : reader.GetFloat(9),
                        MinLevelOverride   = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
                        MaxLevelOverride   = reader.IsDBNull(11) ? (int?)null : reader.GetInt32(11),
                    });
        }

        var rows = new List<SpawnTableSnapshot>(order.Count);
        foreach (var id in order) rows.Add(byId[id]);
        return rows;
    }
}
