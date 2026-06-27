using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// The single shared named-threshold ladder (M2.6, DC4) — KOS … Ally with MinScore cutoffs. One ordered
/// list, GET to read and PUT to replace wholesale. All factions evaluate against this one table.
/// </summary>
[ApiController]
[Route("api/thresholds")]
public class ThresholdsController : ControllerBase
{
    readonly ContentDbContext _db;

    public ThresholdsController(ContentDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FactionThreshold>>> GetAll()
        => await _db.FactionThresholds.OrderBy(t => t.SortOrder).ThenBy(t => t.MinScore).ToListAsync();

    [HttpPut]
    public async Task<ActionResult<IEnumerable<FactionThreshold>>> Replace(List<FactionThreshold> input)
    {
        // Replace the whole ladder. Re-number sort_order from the incoming order so it stays contiguous.
        _db.FactionThresholds.RemoveRange(await _db.FactionThresholds.ToListAsync());

        var rows = new List<FactionThreshold>();
        int order = 0;
        foreach (var t in input ?? new List<FactionThreshold>())
        {
            var name = (t.Name ?? "").Trim();
            if (string.IsNullOrEmpty(name) || rows.Any(r => r.Name == name)) continue;
            rows.Add(new FactionThreshold { Name = name, MinScore = t.MinScore, SortOrder = order++ });
        }
        _db.FactionThresholds.AddRange(rows);
        await _db.SaveChangesAsync();
        return rows;
    }
}
