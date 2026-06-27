using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// CRUD over spawn tables (M2.7.2). Exposes <see cref="SpawnTableDto"/> (header + inlined timer + ordered
/// entries); entry rows are managed internally. Keyed by string spawn_table_id.
/// </summary>
[ApiController]
[Route("api/spawn-tables")]
public class SpawnTablesController : ControllerBase
{
    readonly ContentDbContext _db;

    public SpawnTablesController(ContentDbContext db) => _db = db;

    static SpawnTableDto ToDto(SpawnTable t) => new()
    {
        SpawnTableId = t.SpawnTableId,
        DisplayName = t.DisplayName,
        TimerBaseSeconds = t.TimerBaseSeconds,
        TimerVariance = t.TimerVariance,
        Entries = t.Entries.OrderBy(e => e.SortOrder)
            .Select(e => new SpawnEntryDto { MobId = e.MobId, Weight = e.Weight, GroupSize = e.GroupSize }).ToList(),
    };

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SpawnTableDto>>> GetAll()
    {
        var rows = await _db.SpawnTables.Include(t => t.Entries).OrderBy(t => t.SpawnTableId).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    [HttpGet("{spawnTableId}")]
    public async Task<ActionResult<SpawnTableDto>> Get(string spawnTableId)
    {
        var row = await _db.SpawnTables.Include(t => t.Entries).FirstOrDefaultAsync(t => t.SpawnTableId == spawnTableId);
        return row is null ? NotFound() : ToDto(row);
    }

    [HttpPost]
    public async Task<ActionResult<SpawnTableDto>> Create(SpawnTableDto input)
    {
        input.SpawnTableId = (input.SpawnTableId ?? "").Trim();
        if (string.IsNullOrEmpty(input.SpawnTableId))
            return BadRequest("spawn_table_id is required.");
        if (await _db.SpawnTables.AnyAsync(t => t.SpawnTableId == input.SpawnTableId))
            return Conflict($"A spawn table with id '{input.SpawnTableId}' already exists.");

        var row = new SpawnTable
        {
            SpawnTableId = input.SpawnTableId,
            DisplayName = input.DisplayName ?? "",
            TimerBaseSeconds = input.TimerBaseSeconds,
            TimerVariance = input.TimerVariance,
            UpdatedAt = DateTime.UtcNow,
            Entries = BuildEntries(input),
        };
        _db.SpawnTables.Add(row);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { spawnTableId = row.SpawnTableId }, ToDto(row));
    }

    [HttpPut("{spawnTableId}")]
    public async Task<ActionResult<SpawnTableDto>> Update(string spawnTableId, SpawnTableDto input)
    {
        var row = await _db.SpawnTables.Include(t => t.Entries).FirstOrDefaultAsync(t => t.SpawnTableId == spawnTableId);
        if (row is null) return NotFound();

        row.DisplayName = input.DisplayName ?? "";
        row.TimerBaseSeconds = input.TimerBaseSeconds;
        row.TimerVariance = input.TimerVariance;
        row.UpdatedAt = DateTime.UtcNow;
        input.SpawnTableId = spawnTableId;
        row.Entries.Clear();
        foreach (var e in BuildEntries(input)) row.Entries.Add(e);

        await _db.SaveChangesAsync();
        return ToDto(row);
    }

    [HttpDelete("{spawnTableId}")]
    public async Task<IActionResult> Delete(string spawnTableId)
    {
        var row = await _db.SpawnTables.FindAsync(spawnTableId);
        if (row is null) return NotFound();
        _db.SpawnTables.Remove(row); // entries cascade (FK ON DELETE CASCADE)
        await _db.SaveChangesAsync();
        return NoContent();
    }

    static List<SpawnTableEntry> BuildEntries(SpawnTableDto input)
    {
        var rows = new List<SpawnTableEntry>();
        int order = 0;
        foreach (var e in input.Entries ?? new List<SpawnEntryDto>())
        {
            var mob = (e.MobId ?? "").Trim();
            if (string.IsNullOrEmpty(mob)) continue;
            rows.Add(new SpawnTableEntry
            {
                SpawnTableId = input.SpawnTableId,
                MobId = mob,
                Weight = e.Weight < 0 ? 0 : e.Weight,
                GroupSize = e.GroupSize < 1 ? 1 : e.GroupSize,
                SortOrder = order++,
            });
        }
        return rows;
    }
}
