using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>CRUD over <c>races</c> (M2.10). Flat entity keyed by string race_id. Mapping-only EF, no auth.</summary>
[ApiController]
[Route("api/races")]
public class RacesController : ControllerBase
{
    readonly ContentDbContext _db;

    public RacesController(ContentDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Race>>> GetAll() =>
        await _db.Races.OrderBy(r => r.RaceId).ToListAsync();

    [HttpGet("{raceId}")]
    public async Task<ActionResult<Race>> Get(string raceId)
    {
        var row = await _db.Races.FindAsync(raceId);
        return row is null ? NotFound() : row;
    }

    [HttpPost]
    public async Task<ActionResult<Race>> Create(Race input)
    {
        input.RaceId = (input.RaceId ?? "").Trim();
        if (string.IsNullOrEmpty(input.RaceId))
            return BadRequest("race_id is required.");
        if (await _db.Races.AnyAsync(r => r.RaceId == input.RaceId))
            return Conflict($"A race with id '{input.RaceId}' already exists.");

        input.UpdatedAt = DateTime.UtcNow;
        _db.Races.Add(input);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { raceId = input.RaceId }, input);
    }

    [HttpPut("{raceId}")]
    public async Task<ActionResult<Race>> Update(string raceId, Race input)
    {
        var row = await _db.Races.FindAsync(raceId);
        if (row is null) return NotFound();

        row.RaceName = input.RaceName;
        row.XpModifier = input.XpModifier;
        row.StrMod = input.StrMod;
        row.StaMod = input.StaMod;
        row.AgiMod = input.AgiMod;
        row.DexMod = input.DexMod;
        row.IntMod = input.IntMod;
        row.WisMod = input.WisMod;
        row.ChaMod = input.ChaMod;
        row.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return row;
    }

    [HttpDelete("{raceId}")]
    public async Task<IActionResult> Delete(string raceId)
    {
        var row = await _db.Races.FindAsync(raceId);
        if (row is null) return NotFound();
        _db.Races.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
