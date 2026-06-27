using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// CRUD over conversation keyword sets (M2.4). Exposes a nested <see cref="ConversationSetDto"/>
/// (set → keywords → unlock list) the Angular editor works with; child rows are managed internally.
/// Keyed by string set_id. Mapping-only EF; no auth (devplan D2).
/// </summary>
[ApiController]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    readonly ContentDbContext _db;

    public ConversationsController(ContentDbContext db) => _db = db;

    static ConversationSetDto ToDto(ConversationSet s) => new()
    {
        SetId = s.SetId,
        DisplayName = s.DisplayName,
        Keywords = s.Keywords.OrderBy(k => k.SortOrder).Select(k => new ConversationKeywordDto
        {
            Keyword = k.Keyword,
            Mode = k.Mode,
            IsOpener = k.IsOpener,
            EndsConversation = k.EndsConversation,
            RequiresUnlock = k.RequiresUnlock,
            Response = k.Response,
            RequiredFactionId = k.RequiredFactionId,
            RequiredStanding = k.RequiredStanding,
            Unlocks = k.Unlocks.Select(u => u.UnlockedKeyword).ToList(),
        }).ToList(),
    };

    IQueryable<ConversationSet> WithChildren() =>
        _db.ConversationSets.Include(s => s.Keywords).ThenInclude(k => k.Unlocks);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConversationSetDto>>> GetAll()
    {
        var rows = await WithChildren().OrderBy(s => s.SetId).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    [HttpGet("{setId}")]
    public async Task<ActionResult<ConversationSetDto>> Get(string setId)
    {
        var row = await WithChildren().FirstOrDefaultAsync(s => s.SetId == setId);
        return row is null ? NotFound() : ToDto(row);
    }

    [HttpPost]
    public async Task<ActionResult<ConversationSetDto>> Create(ConversationSetDto input)
    {
        input.SetId = (input.SetId ?? "").Trim();
        if (string.IsNullOrEmpty(input.SetId))
            return BadRequest("set_id is required.");
        if (await _db.ConversationSets.AnyAsync(s => s.SetId == input.SetId))
            return Conflict($"A conversation set with id '{input.SetId}' already exists.");

        var row = new ConversationSet
        {
            SetId = input.SetId,
            DisplayName = input.DisplayName ?? "",
            UpdatedAt = DateTime.UtcNow,
            Keywords = BuildKeywords(input),
        };
        _db.ConversationSets.Add(row);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { setId = row.SetId }, ToDto(row));
    }

    [HttpPut("{setId}")]
    public async Task<ActionResult<ConversationSetDto>> Update(string setId, ConversationSetDto input)
    {
        var row = await WithChildren().FirstOrDefaultAsync(s => s.SetId == setId);
        if (row is null) return NotFound();

        row.DisplayName = input.DisplayName ?? "";
        row.UpdatedAt = DateTime.UtcNow;
        row.Keywords.Clear(); // cascade-delete orphans; rebuild from the DTO
        input.SetId = setId;
        foreach (var k in BuildKeywords(input)) row.Keywords.Add(k);

        await _db.SaveChangesAsync();
        var reloaded = await WithChildren().FirstAsync(s => s.SetId == setId);
        return ToDto(reloaded);
    }

    [HttpDelete("{setId}")]
    public async Task<IActionResult> Delete(string setId)
    {
        var row = await _db.ConversationSets.FindAsync(setId);
        if (row is null) return NotFound();
        _db.ConversationSets.Remove(row); // keywords + unlocks cascade
        await _db.SaveChangesAsync();
        return NoContent();
    }

    static List<ConversationKeywordRow> BuildKeywords(ConversationSetDto input)
    {
        var rows = new List<ConversationKeywordRow>();
        var kws = input.Keywords ?? new List<ConversationKeywordDto>();
        for (int i = 0; i < kws.Count; i++)
        {
            var k = kws[i];
            if (string.IsNullOrWhiteSpace(k.Keyword)) continue;
            rows.Add(new ConversationKeywordRow
            {
                SetId = input.SetId,
                SortOrder = i,
                Keyword = k.Keyword.Trim(),
                Mode = k.Mode,
                IsOpener = k.IsOpener,
                EndsConversation = k.EndsConversation,
                RequiresUnlock = k.RequiresUnlock,
                Response = k.Response ?? "",
                RequiredFactionId = string.IsNullOrWhiteSpace(k.RequiredFactionId) ? null : k.RequiredFactionId.Trim(),
                RequiredStanding = string.IsNullOrWhiteSpace(k.RequiredStanding) ? null : k.RequiredStanding.Trim(),
                Unlocks = (k.Unlocks ?? new List<string>())
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .Select(u => new ConversationKeywordUnlock { UnlockedKeyword = u.Trim() })
                    .ToList(),
            });
        }
        return rows;
    }
}
