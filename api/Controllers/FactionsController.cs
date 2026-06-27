using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// CRUD over factions (M2.6). Exposes <see cref="FactionDto"/> (faction + ally/hostile id lists + race
/// defaults); the relation + race-default child rows are managed internally. Keyed by string faction_id.
/// </summary>
[ApiController]
[Route("api/factions")]
public class FactionsController : ControllerBase
{
    readonly ContentDbContext _db;

    public FactionsController(ContentDbContext db) => _db = db;

    static FactionDto ToDto(Faction f) => new()
    {
        FactionId = f.FactionId,
        FactionName = f.FactionName,
        AllyIds = f.Relations.Where(r => r.Relation == "ally").Select(r => r.OtherFactionId).ToList(),
        HostileIds = f.Relations.Where(r => r.Relation == "hostile").Select(r => r.OtherFactionId).ToList(),
        RaceDefaults = f.RaceDefaults.Select(d => new RaceDefaultDto { Race = d.Race, Score = d.Score }).ToList(),
    };

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FactionDto>>> GetAll()
    {
        var rows = await _db.Factions.Include(f => f.Relations).Include(f => f.RaceDefaults)
            .OrderBy(f => f.FactionId).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    [HttpGet("{factionId}")]
    public async Task<ActionResult<FactionDto>> Get(string factionId)
    {
        var row = await _db.Factions.Include(f => f.Relations).Include(f => f.RaceDefaults)
            .FirstOrDefaultAsync(f => f.FactionId == factionId);
        return row is null ? NotFound() : ToDto(row);
    }

    [HttpPost]
    public async Task<ActionResult<FactionDto>> Create(FactionDto input)
    {
        input.FactionId = (input.FactionId ?? "").Trim();
        if (string.IsNullOrEmpty(input.FactionId))
            return BadRequest("faction_id is required.");
        if (await _db.Factions.AnyAsync(f => f.FactionId == input.FactionId))
            return Conflict($"A faction with id '{input.FactionId}' already exists.");

        var row = new Faction
        {
            FactionId = input.FactionId,
            FactionName = input.FactionName ?? "",
            UpdatedAt = DateTime.UtcNow,
            Relations = BuildRelations(input),
            RaceDefaults = BuildRaceDefaults(input),
        };
        _db.Factions.Add(row);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { factionId = row.FactionId }, ToDto(row));
    }

    [HttpPut("{factionId}")]
    public async Task<ActionResult<FactionDto>> Update(string factionId, FactionDto input)
    {
        var row = await _db.Factions.Include(f => f.Relations).Include(f => f.RaceDefaults)
            .FirstOrDefaultAsync(f => f.FactionId == factionId);
        if (row is null) return NotFound();

        row.FactionName = input.FactionName ?? "";
        row.UpdatedAt = DateTime.UtcNow;
        input.FactionId = factionId;

        // Replace children wholesale (simplest correct semantics).
        row.Relations.Clear();
        foreach (var r in BuildRelations(input)) row.Relations.Add(r);
        row.RaceDefaults.Clear();
        foreach (var d in BuildRaceDefaults(input)) row.RaceDefaults.Add(d);

        await _db.SaveChangesAsync();
        return ToDto(row);
    }

    [HttpDelete("{factionId}")]
    public async Task<IActionResult> Delete(string factionId)
    {
        var row = await _db.Factions.FindAsync(factionId);
        if (row is null) return NotFound();
        _db.Factions.Remove(row); // relation + race-default rows cascade (FK ON DELETE CASCADE)
        await _db.SaveChangesAsync();
        return NoContent();
    }

    static List<FactionRelation> BuildRelations(FactionDto input)
    {
        var rows = new List<FactionRelation>();
        AddRelations(rows, input.FactionId, input.AllyIds, "ally");
        AddRelations(rows, input.FactionId, input.HostileIds, "hostile");
        return rows;
    }

    static void AddRelations(List<FactionRelation> rows, string factionId, List<string>? ids, string relation)
    {
        foreach (var raw in ids ?? new List<string>())
        {
            var other = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(other) || other == factionId) continue;
            if (rows.Any(r => r.OtherFactionId == other && r.Relation == relation)) continue; // dedupe
            rows.Add(new FactionRelation { FactionId = factionId, OtherFactionId = other, Relation = relation });
        }
    }

    static List<RaceFactionDefault> BuildRaceDefaults(FactionDto input)
    {
        var rows = new List<RaceFactionDefault>();
        foreach (var d in input.RaceDefaults ?? new List<RaceDefaultDto>())
        {
            var race = (d.Race ?? "").Trim();
            if (string.IsNullOrEmpty(race)) continue;
            if (rows.Any(r => r.Race == race)) continue; // (race, faction) is the PK — keep the first
            rows.Add(new RaceFactionDefault { Race = race, FactionId = input.FactionId, Score = d.Score });
        }
        return rows;
    }
}
