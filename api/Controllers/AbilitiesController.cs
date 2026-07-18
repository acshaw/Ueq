using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// CRUD over abilities (M2.9). Exposes <see cref="AbilityDto"/> (header + tag ids / cooldown links /
/// effects); child rows are managed internally. Keyed by string ability_id.
/// </summary>
[ApiController]
[Route("api/abilities")]
public class AbilitiesController : ControllerBase
{
    readonly ContentDbContext _db;

    public AbilitiesController(ContentDbContext db) => _db = db;

    static AbilityDto ToDto(Ability a) => new()
    {
        AbilityId = a.AbilityId,
        DisplayName = a.DisplayName,
        Description = a.Description,
        TargetingType = a.TargetingType,
        Range = a.Range,
        CastTime = a.CastTime,
        ManaCost = a.ManaCost,
        AnimTrigger = a.AnimTrigger,
        TagIds = a.Tags.OrderBy(t => t.SortOrder).Select(t => t.TagId).ToList(),
        CooldownLinks = a.CooldownLinks.OrderBy(l => l.SortOrder)
            .Select(l => new AbilityCooldownLinkDto { TagId = l.TagId, Duration = l.Duration }).ToList(),
        Effects = a.Effects.OrderBy(e => e.SortOrder)
            .Select(e => new AbilityEffectDto { EffectType = e.EffectType, BaseAmount = e.BaseAmount, ScalingStat = e.ScalingStat, ScalingFactor = e.ScalingFactor }).ToList(),
    };

    IQueryable<Ability> WithChildren() =>
        _db.Abilities.Include(a => a.Tags).Include(a => a.CooldownLinks).Include(a => a.Effects);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AbilityDto>>> GetAll()
    {
        var rows = await WithChildren().OrderBy(a => a.AbilityId).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    [HttpGet("{abilityId}")]
    public async Task<ActionResult<AbilityDto>> Get(string abilityId)
    {
        var row = await WithChildren().FirstOrDefaultAsync(a => a.AbilityId == abilityId);
        return row is null ? NotFound() : ToDto(row);
    }

    [HttpPost]
    public async Task<ActionResult<AbilityDto>> Create(AbilityDto input)
    {
        input.AbilityId = (input.AbilityId ?? "").Trim();
        if (string.IsNullOrEmpty(input.AbilityId))
            return BadRequest("ability_id is required.");
        if (await _db.Abilities.AnyAsync(a => a.AbilityId == input.AbilityId))
            return Conflict($"An ability with id '{input.AbilityId}' already exists.");

        var row = new Ability
        {
            AbilityId = input.AbilityId,
            DisplayName = input.DisplayName ?? "",
            Description = input.Description ?? "",
            TargetingType = input.TargetingType,
            Range = input.Range,
            CastTime = input.CastTime,
            ManaCost = input.ManaCost,
            AnimTrigger = input.AnimTrigger ?? "",
            UpdatedAt = DateTime.UtcNow,
        };
        Fill(row, input);
        _db.Abilities.Add(row);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { abilityId = row.AbilityId }, ToDto(row));
    }

    [HttpPut("{abilityId}")]
    public async Task<ActionResult<AbilityDto>> Update(string abilityId, AbilityDto input)
    {
        var row = await WithChildren().FirstOrDefaultAsync(a => a.AbilityId == abilityId);
        if (row is null) return NotFound();

        row.DisplayName = input.DisplayName ?? "";
        row.Description = input.Description ?? "";
        row.TargetingType = input.TargetingType;
        row.Range = input.Range;
        row.CastTime = input.CastTime;
        row.ManaCost = input.ManaCost;
        row.AnimTrigger = input.AnimTrigger ?? "";
        row.UpdatedAt = DateTime.UtcNow;

        input.AbilityId = abilityId;
        row.Tags.Clear();
        row.CooldownLinks.Clear();
        row.Effects.Clear();
        Fill(row, input);

        await _db.SaveChangesAsync();
        return ToDto(row);
    }

    [HttpDelete("{abilityId}")]
    public async Task<IActionResult> Delete(string abilityId)
    {
        var row = await _db.Abilities.FindAsync(abilityId);
        if (row is null) return NotFound();
        _db.Abilities.Remove(row); // child rows cascade (FK ON DELETE CASCADE)
        await _db.SaveChangesAsync();
        return NoContent();
    }

    static void Fill(Ability row, AbilityDto input)
    {
        int i = 0;
        foreach (var tagId in input.TagIds ?? new List<string>())
        {
            var id = (tagId ?? "").Trim();
            if (string.IsNullOrEmpty(id)) continue;
            row.Tags.Add(new AbilityDefinitionTag { AbilityId = input.AbilityId, TagId = id, SortOrder = i++ });
        }
        i = 0;
        foreach (var l in input.CooldownLinks ?? new List<AbilityCooldownLinkDto>())
        {
            var id = (l.TagId ?? "").Trim();
            if (string.IsNullOrEmpty(id)) continue;
            row.CooldownLinks.Add(new AbilityCooldownLink { AbilityId = input.AbilityId, TagId = id, Duration = l.Duration, SortOrder = i++ });
        }
        i = 0;
        foreach (var e in input.Effects ?? new List<AbilityEffectDto>())
        {
            var type = (e.EffectType ?? "").Trim();
            if (string.IsNullOrEmpty(type)) continue;
            row.Effects.Add(new AbilityEffectRow
            {
                AbilityId = input.AbilityId, EffectType = type, BaseAmount = e.BaseAmount,
                ScalingStat = e.ScalingStat, ScalingFactor = e.ScalingFactor, SortOrder = i++,
            });
        }
    }
}
