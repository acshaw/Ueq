using Npgsql;

/// <summary>
/// Repository over the single <c>world_clock_settings</c> row (5.12 follow-up, extended 8.1 for the
/// persisted calendar). Read-heavy — the day-length/lunar-cycle/fog tunables and the calendar's Age/
/// elapsed-seconds state are all read once at boot (highest priority in <see cref="WorldClock"/>'s
/// DB -> Resources asset -> in-memory-default fallback chain for the tunables; the calendar fields have
/// no such fallback tier, they're plain persisted state — see <see cref="WorldClock.SetPersistedCalendarState"/>).
/// Also reads/writes the two small fixed-cardinality day/month name tables (8.1.6).
/// </summary>
public sealed class WorldClockSettingsRepository : IRepository
{
    public (float dayLengthMinutes, float lunarCycleDays, float fogStartDistance, float fogEndDistance,
             double calendarElapsedSeconds, int age, long ageStartedElapsedDays, int ageStartingYearDisplay)?
        Load(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand(
            "SELECT day_length_minutes, lunar_cycle_days, fog_start_distance, fog_end_distance, " +
            "calendar_elapsed_seconds, age, age_started_elapsed_days, age_starting_year_display " +
            "FROM world_clock_settings WHERE id = 1", conn, tx);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return (reader.GetFloat(0), reader.GetFloat(1), reader.GetFloat(2), reader.GetFloat(3),
                reader.GetDouble(4), reader.GetInt32(5), reader.GetInt64(6), reader.GetInt32(7));
    }

    /// <summary>8.1.1/8.1.2 (CAL2): persist the current calendar state. Called on the 90s autosave tick,
    /// on clean shutdown, and immediately after ServerSetAge. Direct synchronous write, same convention
    /// as <see cref="Load"/> — a single-row settings write this infrequent has no latency profile worth
    /// routing through 1.2's async PersistenceService queue.</summary>
    public void SaveCalendarState(NpgsqlConnection conn, double calendarElapsedSeconds, int age,
        long ageStartedElapsedDays, int ageStartingYearDisplay)
    {
        using var cmd = new NpgsqlCommand(
            "UPDATE world_clock_settings SET calendar_elapsed_seconds = @elapsed, age = @age, " +
            "age_started_elapsed_days = @ageStarted, age_starting_year_display = @ageYear WHERE id = 1", conn);
        cmd.Parameters.AddWithValue("elapsed", calendarElapsedSeconds);
        cmd.Parameters.AddWithValue("age", age);
        cmd.Parameters.AddWithValue("ageStarted", ageStartedElapsedDays);
        cmd.Parameters.AddWithValue("ageYear", ageStartingYearDisplay);
        cmd.ExecuteNonQuery();
    }

    /// <summary>8.1.6 (CAL8): the 7 day names, indexed 0..6 for day 1..7. Null entries (a gap in the
    /// table) fall back to WorldClock's own numbered placeholder — this just returns what's there.</summary>
    public string[] LoadDayNames(NpgsqlConnection conn)
    {
        var names = new string[WorldClock.DaysPerWeek];
        using var cmd = new NpgsqlCommand("SELECT day_index, name FROM world_clock_day_names", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int idx = reader.GetInt16(0) - 1;
            if (idx >= 0 && idx < names.Length) names[idx] = reader.GetString(1);
        }
        return names;
    }

    /// <summary>8.1.6 (CAL8): the 13 month names, indexed 0..12 for month 1..13.</summary>
    public string[] LoadMonthNames(NpgsqlConnection conn)
    {
        var names = new string[WorldClock.MonthsPerYear];
        using var cmd = new NpgsqlCommand("SELECT month_index, name FROM world_clock_month_names", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int idx = reader.GetInt16(0) - 1;
            if (idx >= 0 && idx < names.Length) names[idx] = reader.GetString(1);
        }
        return names;
    }
}
