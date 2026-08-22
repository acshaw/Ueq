using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// CRUD over classes (M2.10). Exposes <see cref="ClassDto"/> (header + ordered starting-ability id
/// list); child rows are managed internally. Keyed by string class_id.
/// </summary>
[ApiController]
[Route("api/classes")]
public class ClassesController : ControllerBase
{
    readonly ContentDbContext _db;

    public ClassesController(ContentDbContext db) => _db = db;

    static ClassDto ToDto(Class c) => new()
    {
        ClassId = c.ClassId, ClassName = c.ClassName, XpModifier = c.XpModifier,
        BaseStr = c.BaseStr, BaseSta = c.BaseSta, BaseAgi = c.BaseAgi, BaseDex = c.BaseDex,
        BaseInt = c.BaseInt, BaseWis = c.BaseWis, BaseCha = c.BaseCha,
        ClassBaseHP = c.ClassBaseHP, HpPerLevel = c.HpPerLevel, StaCap = c.StaCap,
        BaseStaRatio = c.BaseStaRatio, StaGrowthRate = c.StaGrowthRate,
        ManaStatType = c.ManaStatType, ClassBaseMana = c.ClassBaseMana, ManaPerLevel = c.ManaPerLevel,
        ManaCap = c.ManaCap, BaseManaRatio = c.BaseManaRatio, ManaGrowthRate = c.ManaGrowthRate,
        StartingAbilityIds = c.StartingAbilities.OrderBy(a => a.SortOrder).Select(a => a.AbilityId).ToList(),
    };

    IQueryable<Class> WithChildren() => _db.Classes.Include(c => c.StartingAbilities);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClassDto>>> GetAll()
    {
        var rows = await WithChildren().OrderBy(c => c.ClassId).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    [HttpGet("{classId}")]
    public async Task<ActionResult<ClassDto>> Get(string classId)
    {
        var row = await WithChildren().FirstOrDefaultAsync(c => c.ClassId == classId);
        return row is null ? NotFound() : ToDto(row);
    }

    [HttpPost]
    public async Task<ActionResult<ClassDto>> Create(ClassDto input)
    {
        input.ClassId = (input.ClassId ?? "").Trim();
        if (string.IsNullOrEmpty(input.ClassId))
            return BadRequest("class_id is required.");
        if (await _db.Classes.AnyAsync(c => c.ClassId == input.ClassId))
            return Conflict($"A class with id '{input.ClassId}' already exists.");

        var row = new Class { ClassId = input.ClassId };
        Fill(row, input);
        _db.Classes.Add(row);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { classId = row.ClassId }, ToDto(row));
    }

    [HttpPut("{classId}")]
    public async Task<ActionResult<ClassDto>> Update(string classId, ClassDto input)
    {
        var row = await WithChildren().FirstOrDefaultAsync(c => c.ClassId == classId);
        if (row is null) return NotFound();

        input.ClassId = classId;
        row.StartingAbilities.Clear();
        Fill(row, input);

        await _db.SaveChangesAsync();
        return ToDto(row);
    }

    [HttpDelete("{classId}")]
    public async Task<IActionResult> Delete(string classId)
    {
        var row = await _db.Classes.FindAsync(classId);
        if (row is null) return NotFound();
        _db.Classes.Remove(row); // child rows cascade (FK ON DELETE CASCADE)
        await _db.SaveChangesAsync();
        return NoContent();
    }

    static void Fill(Class row, ClassDto input)
    {
        row.UpdatedAt = DateTime.UtcNow;
        row.ClassName = input.ClassName ?? "";
        row.XpModifier = input.XpModifier;
        row.BaseStr = input.BaseStr; row.BaseSta = input.BaseSta; row.BaseAgi = input.BaseAgi;
        row.BaseDex = input.BaseDex; row.BaseInt = input.BaseInt; row.BaseWis = input.BaseWis; row.BaseCha = input.BaseCha;
        row.ClassBaseHP = input.ClassBaseHP; row.HpPerLevel = input.HpPerLevel; row.StaCap = input.StaCap;
        row.BaseStaRatio = input.BaseStaRatio; row.StaGrowthRate = input.StaGrowthRate;
        row.ManaStatType = input.ManaStatType; row.ClassBaseMana = input.ClassBaseMana;
        row.ManaPerLevel = input.ManaPerLevel; row.ManaCap = input.ManaCap;
        row.BaseManaRatio = input.BaseManaRatio; row.ManaGrowthRate = input.ManaGrowthRate;

        int i = 0;
        foreach (var abilityId in input.StartingAbilityIds ?? new List<string>())
        {
            var id = (abilityId ?? "").Trim();
            if (string.IsNullOrEmpty(id)) continue;
            row.StartingAbilities.Add(new ClassStartingAbility { ClassId = input.ClassId, AbilityId = id, SortOrder = i++ });
        }
    }
}
