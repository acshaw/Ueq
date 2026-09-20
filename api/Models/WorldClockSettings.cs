namespace Ueq.ContentApi.Models;

/// <summary>
/// EF entity for the single <c>world_clock_settings</c> row (5.12 follow-up, extended for distance fog
/// and — 8.1 — the persisted calendar). Day-length/lunar-cycle/fog stay editable via this row as before;
/// the calendar fields (CalendarElapsedSeconds/Age/AgeStartedElapsedDays/AgeStartingYearDisplay) are
/// exposed for display only — <c>WorldClockSettingsController.Update</c> deliberately never writes them,
/// since <c>/set-age</c> in-game is the sole live-mutation path (editing here only applies on the next
/// server restart, which would silently conflict with /set-age's immediate effect). Mapping-only; SQL
/// runner owns the schema. Id is always 1 (a singleton row, not a list of entities like most other
/// content types).
/// </summary>
public class WorldClockSettings
{
    public int      Id               { get; set; } = 1;
    public float    DayLengthMinutes { get; set; }
    public float    LunarCycleDays   { get; set; }
    public float    FogStartDistance { get; set; }
    public float    FogEndDistance   { get; set; }
    public DateTime UpdatedAt        { get; set; }

    public double   CalendarElapsedSeconds { get; set; }
    public int      Age                    { get; set; } = 1;
    public long     AgeStartedElapsedDays  { get; set; }
    public int      AgeStartingYearDisplay { get; set; } = 1372;
}

/// <summary>EF entity for <c>world_clock_day_names</c> (8.1.6) — 7 rows, fixed cardinality (the calendar
/// math fixes the day count forever), renameable from the web editor without a rebuild.</summary>
public class WorldClockDayName
{
    public int    DayIndex { get; set; } // 1-7
    public string Name     { get; set; } = string.Empty;
}

/// <summary>EF entity for <c>world_clock_month_names</c> (8.1.6) — 13 rows, fixed cardinality.</summary>
public class WorldClockMonthName
{
    public int    MonthIndex { get; set; } // 1-13
    public string Name       { get; set; } = string.Empty;
}

/// <summary>Editor-facing shape: the settings row plus the two name lists, so the World Clock Editor
/// page is one GET/PUT round trip instead of three. Not an EF entity — assembled/decomposed by the
/// controller.</summary>
public class WorldClockSettingsDto
{
    public int      Id               { get; set; } = 1;
    public float    DayLengthMinutes { get; set; }
    public float    LunarCycleDays   { get; set; }
    public float    FogStartDistance { get; set; }
    public float    FogEndDistance   { get; set; }
    public DateTime UpdatedAt        { get; set; }

    // Display-only — see WorldClockSettings' own doc comment for why Update never writes these.
    public int  Age                    { get; set; } = 1;
    public int  AgeStartingYearDisplay { get; set; } = 1372;

    public List<string> DayNames   { get; set; } = new();
    public List<string> MonthNames { get; set; } = new();
}
