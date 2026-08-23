using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// Read + narrow-write over <c>world_placements</c> (2.7.3, Stage C). Unlike every other content
/// controller, this is <b>not</b> the authoring path — Unity's Editor sync/import tools write these rows
/// directly to Postgres (devplan WP4). This controller exists only for the web Placement Editor: list
/// every placement (optionally filtered by zone, for the grid) and let an admin tweak a <c>SpawnPoint</c>'s
/// non-spatial config without reopening Unity (WP7). There is deliberately no <c>POST</c> — a placement's
/// position has no meaningful value from a web form, so placements are only ever created via Unity.
/// <c>Update</c> only ever touches <c>Data</c>; <c>ZoneId</c>/<c>MarkerType</c>/position/rotation are
/// Unity-authored and ignored even if a client sends different values for them.
/// </summary>
[ApiController]
[Route("api/world-placements")]
public class WorldPlacementsController : ControllerBase
{
    readonly ContentDbContext _db;

    public WorldPlacementsController(ContentDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorldPlacement>>> GetAll([FromQuery] string? zoneId)
    {
        var query = _db.WorldPlacements.AsQueryable();
        if (!string.IsNullOrWhiteSpace(zoneId))
            query = query.Where(p => p.ZoneId == zoneId);
        return await query.OrderBy(p => p.ZoneId).ThenBy(p => p.MarkerType).ToListAsync();
    }

    [HttpGet("{placementId:guid}")]
    public async Task<ActionResult<WorldPlacement>> Get(Guid placementId)
    {
        var row = await _db.WorldPlacements.FindAsync(placementId);
        return row is null ? NotFound() : row;
    }

    [HttpPut("{placementId:guid}")]
    public async Task<ActionResult<WorldPlacement>> Update(Guid placementId, WorldPlacement input)
    {
        var row = await _db.WorldPlacements.FindAsync(placementId);
        if (row is null) return NotFound();

        row.Data = string.IsNullOrWhiteSpace(input.Data) ? "{}" : input.Data;
        row.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return row;
    }

    [HttpDelete("{placementId:guid}")]
    public async Task<IActionResult> Delete(Guid placementId)
    {
        var row = await _db.WorldPlacements.FindAsync(placementId);
        if (row is null) return NotFound();
        _db.WorldPlacements.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
