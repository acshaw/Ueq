using Npgsql;

/// <summary>
/// Read-only repository over the single <c>world_clock_settings</c> row (5.12 follow-up) — lets the
/// day-length/lunar-cycle tunables be web-authored instead of only living on a Unity
/// <see cref="WorldClockSettings"/> Resources asset. Highest priority in <see cref="WorldClock"/>'s
/// DB -> Resources asset -> in-memory-default fallback chain; returns null if the row is missing, which
/// falls through to that existing chain unchanged.
/// </summary>
public sealed class WorldClockSettingsRepository : IRepository
{
    public (float dayLengthMinutes, float lunarCycleDays, float fogStartDistance, float fogEndDistance)?
        Load(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand(
            "SELECT day_length_minutes, lunar_cycle_days, fog_start_distance, fog_end_distance " +
            "FROM world_clock_settings WHERE id = 1", conn, tx);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return (reader.GetFloat(0), reader.GetFloat(1), reader.GetFloat(2), reader.GetFloat(3));
    }
}
