using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// The single shared XP curve (M2.7) — one row per level. GET to read (ordered), PUT to replace the whole
/// curve. Levels are renumbered 1..N from the incoming order so the table stays contiguous.
/// </summary>
[ApiController]
[Route("api/xp-levels")]
public class XpController : ControllerBase
{
    readonly ContentDbContext _db;

    public XpController(ContentDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<XpLevel>>> GetAll()
        => await _db.XpLevels.OrderBy(x => x.Level).ToListAsync();

    [HttpPut]
    public async Task<ActionResult<IEnumerable<XpLevel>>> Replace(List<XpLevel> input)
    {
        _db.XpLevels.RemoveRange(await _db.XpLevels.ToListAsync());

        var rows = new List<XpLevel>();
        int level = 1;
        foreach (var x in input ?? new List<XpLevel>())
            rows.Add(new XpLevel { Level = level++, XpToNext = x.XpToNext });

        _db.XpLevels.AddRange(rows);
        await _db.SaveChangesAsync();
        return rows;
    }
}
