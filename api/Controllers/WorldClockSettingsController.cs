using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// The single shared day-length/lunar-cycle/fog config (5.12 follow-up) plus — 8.1 — the calendar's
/// display names and a read-only view of Age. GET returns one combined DTO (settings row + name lists),
/// PUT upserts the day-length/lunar-cycle/fog fields and the name lists. The Unity game server only
/// reads the settings row at boot — a web edit to those fields takes effect the next time the server
/// (re)starts, same as every other content type here. <b>Age/CalendarElapsedSeconds/
/// AgeStartedElapsedDays are deliberately never written by this controller</b> — <c>/set-age</c> in-game
/// is the sole live-mutation path for those (see WorldClockSettings' doc comment); editing them here
/// would silently do nothing until a restart, which reads as broken next to a command that's immediate.
/// </summary>
[ApiController]
[Route("api/world-clock-settings")]
public class WorldClockSettingsController : ControllerBase
{
    readonly ContentDbContext _db;

    public WorldClockSettingsController(ContentDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<WorldClockSettingsDto>> Get()
    {
        var row = await _db.WorldClockSettings.FindAsync(1);
        row ??= new WorldClockSettings
        {
            Id = 1, DayLengthMinutes = 50, LunarCycleDays = 28,
            FogStartDistance = 120, FogEndDistance = 520,
            Age = 1, AgeStartingYearDisplay = 1372,
        };

        var dayNames = await _db.WorldClockDayNames.OrderBy(d => d.DayIndex).ToListAsync();
        var monthNames = await _db.WorldClockMonthNames.OrderBy(m => m.MonthIndex).ToListAsync();
        return ToDto(row, dayNames, monthNames);
    }

    [HttpPut]
    public async Task<ActionResult<WorldClockSettingsDto>> Update(WorldClockSettingsDto input)
    {
        float dayLength = Math.Max(1f, input?.DayLengthMinutes ?? 50f);
        float lunarDays = Math.Max(0.5f, input?.LunarCycleDays ?? 28f);
        float fogStart  = Math.Max(0f, input?.FogStartDistance ?? 120f);
        // End must clear start by at least a small margin or Linear fog degenerates to a hard cutoff.
        float fogEnd    = Math.Max(fogStart + 10f, input?.FogEndDistance ?? 520f);

        var row = await _db.WorldClockSettings.FindAsync(1);
        if (row == null)
        {
            row = new WorldClockSettings { Id = 1, Age = 1, AgeStartingYearDisplay = 1372 };
            _db.WorldClockSettings.Add(row);
        }
        row.DayLengthMinutes = dayLength;
        row.LunarCycleDays = lunarDays;
        row.FogStartDistance = fogStart;
        row.FogEndDistance = fogEnd;
        row.UpdatedAt = DateTime.UtcNow;
        // Age/CalendarElapsedSeconds/AgeStartedElapsedDays intentionally untouched — see class doc comment.

        await SaveDayNames(input?.DayNames);
        await SaveMonthNames(input?.MonthNames);
        await _db.SaveChangesAsync();

        var dayNames = await _db.WorldClockDayNames.OrderBy(d => d.DayIndex).ToListAsync();
        var monthNames = await _db.WorldClockMonthNames.OrderBy(m => m.MonthIndex).ToListAsync();
        return ToDto(row, dayNames, monthNames);
    }

    static WorldClockSettingsDto ToDto(
        WorldClockSettings row, List<WorldClockDayName> dayNames, List<WorldClockMonthName> monthNames)
        => new()
        {
            Id = row.Id,
            DayLengthMinutes = row.DayLengthMinutes,
            LunarCycleDays = row.LunarCycleDays,
            FogStartDistance = row.FogStartDistance,
            FogEndDistance = row.FogEndDistance,
            UpdatedAt = row.UpdatedAt,
            Age = row.Age,
            AgeStartingYearDisplay = row.AgeStartingYearDisplay,
            DayNames = FillGaps(dayNames.Select(d => d.Name).ToList(), 7, "Day"),
            MonthNames = FillGaps(monthNames.Select(m => m.Name).ToList(), 13, "Month"),
        };

    // A fresh migration with no seed run yet (or a row deleted) shouldn't hand the editor a
    // shorter-than-expected list — pad with an obvious placeholder rather than erroring.
    static List<string> FillGaps(List<string> names, int expected, string placeholderPrefix)
    {
        while (names.Count < expected) names.Add($"{placeholderPrefix} {names.Count + 1}");
        return names;
    }

    async Task SaveDayNames(List<string>? input)
    {
        if (input == null) return;
        for (int i = 0; i < input.Count && i < 7; i++)
        {
            var name = (input[i] ?? "").Trim();
            if (string.IsNullOrEmpty(name)) continue;

            int index = i + 1;
            var existing = await _db.WorldClockDayNames.FindAsync(index);
            if (existing == null) _db.WorldClockDayNames.Add(new WorldClockDayName { DayIndex = index, Name = name });
            else existing.Name = name;
        }
    }

    async Task SaveMonthNames(List<string>? input)
    {
        if (input == null) return;
        for (int i = 0; i < input.Count && i < 13; i++)
        {
            var name = (input[i] ?? "").Trim();
            if (string.IsNullOrEmpty(name)) continue;

            int index = i + 1;
            var existing = await _db.WorldClockMonthNames.FindAsync(index);
            if (existing == null) _db.WorldClockMonthNames.Add(new WorldClockMonthName { MonthIndex = index, Name = name });
            else existing.Name = name;
        }
    }
}
