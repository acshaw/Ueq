namespace Ueq.ContentApi.Models;

/// <summary>
/// EF entity for the single <c>world_clock_settings</c> row (5.12 follow-up, extended for distance fog) —
/// day-length/lunar-cycle tunables (previously only a Unity Resources asset) plus fog start/end distance
/// (previously only a client-local Inspector field on SkyDriver). Mapping-only; SQL runner owns the
/// schema. Id is always 1 (a singleton row, not a list of entities like most other content types).
/// </summary>
public class WorldClockSettings
{
    public int      Id               { get; set; } = 1;
    public float    DayLengthMinutes { get; set; }
    public float    LunarCycleDays   { get; set; }
    public float    FogStartDistance { get; set; }
    public float    FogEndDistance   { get; set; }
    public DateTime UpdatedAt        { get; set; }
}
