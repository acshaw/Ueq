using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// CRUD over the <c>content_ping</c> smoke table (2.1). Thin by design — this is a data pipe for
/// the Angular editor, not a place for game logic. Every real content type (items = 2.2) copies
/// this controller shape. No auth (trusted local users only, devplan D2).
/// </summary>
[ApiController]
[Route("api/content-ping")]
public class ContentPingController : ControllerBase
{
    readonly ContentDbContext _db;

    public ContentPingController(ContentDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContentPing>>> GetAll() =>
        await _db.ContentPings.OrderBy(p => p.Id).ToListAsync();

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ContentPing>> Get(long id)
    {
        var row = await _db.ContentPings.FindAsync(id);
        return row is null ? NotFound() : row;
    }

    [HttpPost]
    public async Task<ActionResult<ContentPing>> Create(ContentPing input)
    {
        var row = new ContentPing { Label = input.Label, UpdatedAt = DateTime.UtcNow };
        _db.ContentPings.Add(row);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = row.Id }, row);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ContentPing>> Update(long id, ContentPing input)
    {
        var row = await _db.ContentPings.FindAsync(id);
        if (row is null) return NotFound();
        row.Label = input.Label;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return row;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var row = await _db.ContentPings.FindAsync(id);
        if (row is null) return NotFound();
        _db.ContentPings.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
