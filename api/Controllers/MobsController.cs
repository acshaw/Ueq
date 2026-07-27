using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// CRUD over mobs (M2.5). Flat entity keyed by string mob_id; references other content by id
/// (faction/conversation/loot/vendor) + a registered spawnable prefab name. Mapping-only EF, no auth.
/// </summary>
[ApiController]
[Route("api/mobs")]
public class MobsController : ControllerBase
{
    readonly ContentDbContext _db;

    public MobsController(ContentDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Mob>>> GetAll() =>
        await _db.Mobs.Include(m => m.FactionHits.OrderBy(h => h.SortOrder))
            .OrderBy(m => m.MobId).ToListAsync();

    [HttpGet("{mobId}")]
    public async Task<ActionResult<Mob>> Get(string mobId)
    {
        var row = await _db.Mobs.Include(m => m.FactionHits.OrderBy(h => h.SortOrder))
            .FirstOrDefaultAsync(m => m.MobId == mobId);
        return row is null ? NotFound() : row;
    }

    [HttpPost]
    public async Task<ActionResult<Mob>> Create(Mob input)
    {
        input.MobId = (input.MobId ?? "").Trim();
        if (string.IsNullOrEmpty(input.MobId))
            return BadRequest("mob_id is required.");
        if (await _db.Mobs.AnyAsync(m => m.MobId == input.MobId))
            return Conflict($"A mob with id '{input.MobId}' already exists.");

        Normalize(input);
        input.FactionHits = BuildHits(input);
        input.UpdatedAt = DateTime.UtcNow;
        _db.Mobs.Add(input);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { mobId = input.MobId }, input);
    }

    [HttpPut("{mobId}")]
    public async Task<ActionResult<Mob>> Update(string mobId, Mob input)
    {
        var m = await _db.Mobs.Include(x => x.FactionHits).FirstOrDefaultAsync(x => x.MobId == mobId);
        if (m is null) return NotFound();

        m.DisplayName = input.DisplayName;
        m.MobLevel = input.MobLevel;
        m.PrefabAddress = input.PrefabAddress;
        m.MaxHealth = input.MaxHealth;
        m.AttackDamage = input.AttackDamage;
        m.AttackInterval = input.AttackInterval;
        m.AttackRange = input.AttackRange;
        m.MovementType = input.MovementType;
        m.MoveSpeed = input.MoveSpeed;
        m.WanderRadius = input.WanderRadius;
        m.WanderPauseMin = input.WanderPauseMin;
        m.WanderPauseMax = input.WanderPauseMax;
        m.PerceptionRadius = input.PerceptionRadius;
        m.BaseAggroThreat = input.BaseAggroThreat;
        m.FactionId = input.FactionId;
        m.AggroMaxStanding = input.AggroMaxStanding;
        m.WarningMaxStanding = input.WarningMaxStanding;
        m.ConversationSetId = input.ConversationSetId;
        m.LootTableId = input.LootTableId;
        m.XpReward = input.XpReward;
        m.VendorId = input.VendorId;
        m.VendorOpenKeyword = input.VendorOpenKeyword;
        m.WeaponCategory = input.WeaponCategory;
        m.WeaponSkill = input.WeaponSkill;
        m.TierMiss = input.TierMiss;
        m.TierGlancing = input.TierGlancing;
        m.TierHit = input.TierHit;
        m.TierSolid = input.TierSolid;
        m.TierGood = input.TierGood;
        m.TierCritical = input.TierCritical;
        m.TierCrippling = input.TierCrippling;
        m.AttackIsParryable = input.AttackIsParryable;
        m.AvoidanceAgility = input.AvoidanceAgility;
        m.AvoidanceDexterity = input.AvoidanceDexterity;
        m.SocialAggroEnabled = input.SocialAggroEnabled;
        m.SocialAggroRadius = input.SocialAggroRadius;
        Normalize(m);
        m.UpdatedAt = DateTime.UtcNow;

        // Replace the faction-hit children wholesale.
        input.MobId = mobId;
        m.FactionHits.Clear();
        foreach (var h in BuildHits(input)) m.FactionHits.Add(h);

        await _db.SaveChangesAsync();
        return m;
    }

    // Rebuild faction-hit children from the request, setting MobId + a contiguous SortOrder and dropping
    // blank/dup-faction rows (faction_id is effectively one hit per faction per mob in the editor).
    static List<MobFactionHit> BuildHits(Mob input)
    {
        var rows = new List<MobFactionHit>();
        int order = 0;
        foreach (var h in input.FactionHits ?? new List<MobFactionHit>())
        {
            var fid = (h.FactionId ?? "").Trim();
            if (string.IsNullOrEmpty(fid)) continue;
            if (rows.Any(r => r.FactionId == fid)) continue;
            rows.Add(new MobFactionHit { MobId = input.MobId, FactionId = fid, Delta = h.Delta, SortOrder = order++ });
        }
        return rows;
    }

    [HttpDelete("{mobId}")]
    public async Task<IActionResult> Delete(string mobId)
    {
        var m = await _db.Mobs.FindAsync(mobId);
        if (m is null) return NotFound();
        _db.Mobs.Remove(m);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Blank optional id refs become null (so the game treats "" as "no reference").
    static void Normalize(Mob m)
    {
        m.PrefabAddress = Blank(m.PrefabAddress);
        m.FactionId = Blank(m.FactionId);
        m.ConversationSetId = Blank(m.ConversationSetId);
        m.LootTableId = Blank(m.LootTableId);
        m.VendorId = Blank(m.VendorId);
        if (string.IsNullOrWhiteSpace(m.VendorOpenKeyword)) m.VendorOpenKeyword = "wares";
        if (string.IsNullOrWhiteSpace(m.AggroMaxStanding)) m.AggroMaxStanding = "Threatening";
        if (string.IsNullOrWhiteSpace(m.WarningMaxStanding)) m.WarningMaxStanding = "Apprehensive";
    }

    static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
