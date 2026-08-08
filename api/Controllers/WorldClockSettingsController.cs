using Microsoft.AspNetCore.Mvc;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// The single shared day-length/lunar-cycle/fog config (5.12 follow-up). GET returns the one row (or sane
/// defaults if the table is empty), PUT upserts it. The Unity game server only reads this at boot — a web
/// edit takes effect the next time the server (re)starts, same as every other content type here.
/// </summary>
[ApiController]
[Route("api/world-clock-settings")]
public class WorldClockSettingsController : ControllerBase
{
    readonly ContentDbContext _db;

    public WorldClockSettingsController(ContentDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<WorldClockSettings>> Get()
    {
        var row = await _db.WorldClockSettings.FindAsync(1);
        return row ?? new WorldClockSettings
        {
            Id = 1, DayLengthMinutes = 50, LunarCycleDays = 8,
            FogStartDistance = 120, FogEndDistance = 520,
        };
    }

    [HttpPut]
    public async Task<ActionResult<WorldClockSettings>> Update(WorldClockSettings input)
    {
        float dayLength = Math.Max(1f, input?.DayLengthMinutes ?? 50f);
        float lunarDays = Math.Max(0.5f, input?.LunarCycleDays ?? 8f);
        float fogStart  = Math.Max(0f, input?.FogStartDistance ?? 120f);
        // End must clear start by at least a small margin or Linear fog degenerates to a hard cutoff.
        float fogEnd    = Math.Max(fogStart + 10f, input?.FogEndDistance ?? 520f);

        var row = await _db.WorldClockSettings.FindAsync(1);
        if (row == null)
        {
            row = new WorldClockSettings { Id = 1 };
            _db.WorldClockSettings.Add(row);
        }
        row.DayLengthMinutes = dayLength;
        row.LunarCycleDays = lunarDays;
        row.FogStartDistance = fogStart;
        row.FogEndDistance = fogEnd;
        row.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return row;
    }
}
