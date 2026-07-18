using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// CRUD over <c>ability_tags</c> (M2.9) — a flat reference list (id + display name) used by an
/// ability's semantic tags and its cooldown links. Keyed by string tag_id. Mapping-only EF, no auth.
/// </summary>
[ApiController]
[Route("api/ability-tags")]
public class AbilityTagsController : ControllerBase
{
    readonly ContentDbContext _db;

    public AbilityTagsController(ContentDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AbilityTag>>> GetAll() =>
        await _db.AbilityTags.OrderBy(t => t.TagId).ToListAsync();

    [HttpGet("{tagId}")]
    public async Task<ActionResult<AbilityTag>> Get(string tagId)
    {
        var row = await _db.AbilityTags.FindAsync(tagId);
        return row is null ? NotFound() : row;
    }

    [HttpPost]
    public async Task<ActionResult<AbilityTag>> Create(AbilityTag input)
    {
        input.TagId = (input.TagId ?? "").Trim();
        if (string.IsNullOrEmpty(input.TagId))
            return BadRequest("tag_id is required.");
        if (await _db.AbilityTags.AnyAsync(t => t.TagId == input.TagId))
            return Conflict($"A tag with id '{input.TagId}' already exists.");

        _db.AbilityTags.Add(input);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { tagId = input.TagId }, input);
    }

    [HttpPut("{tagId}")]
    public async Task<ActionResult<AbilityTag>> Update(string tagId, AbilityTag input)
    {
        var row = await _db.AbilityTags.FindAsync(tagId);
        if (row is null) return NotFound();

        row.DisplayName = input.DisplayName;
        await _db.SaveChangesAsync();
        return row;
    }

    [HttpDelete("{tagId}")]
    public async Task<IActionResult> Delete(string tagId)
    {
        var row = await _db.AbilityTags.FindAsync(tagId);
        if (row is null) return NotFound();
        _db.AbilityTags.Remove(row); // referencing ability_definition_tags/ability_cooldown_links rows would block this via FK if in use
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
