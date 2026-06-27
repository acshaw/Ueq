using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// CRUD over loot tables (M2.7). Exposes <see cref="LootTableDto"/> (header + items / drop-counts /
/// coin-tiers); child rows are managed internally. Keyed by string loot_table_id.
/// </summary>
[ApiController]
[Route("api/loot-tables")]
public class LootTablesController : ControllerBase
{
    readonly ContentDbContext _db;

    public LootTablesController(ContentDbContext db) => _db = db;

    static LootTableDto ToDto(LootTable t) => new()
    {
        LootTableId = t.LootTableId,
        DisplayName = t.DisplayName,
        Items = t.Items.OrderBy(i => i.SortOrder).Select(i => new LootItemDto { ItemId = i.ItemId, Weight = i.Weight }).ToList(),
        DropCounts = t.DropCounts.OrderBy(d => d.SortOrder).Select(d => new LootDropCountDto { Count = d.Count, Weight = d.Weight }).ToList(),
        CoinTiers = t.CoinTiers.OrderBy(c => c.SortOrder).Select(c => new LootCoinTierDto { MinCopper = c.MinCopper, MaxCopper = c.MaxCopper, Weight = c.Weight }).ToList(),
    };

    IQueryable<LootTable> WithChildren() =>
        _db.LootTables.Include(t => t.Items).Include(t => t.DropCounts).Include(t => t.CoinTiers);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LootTableDto>>> GetAll()
    {
        var rows = await WithChildren().OrderBy(t => t.LootTableId).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    [HttpGet("{lootTableId}")]
    public async Task<ActionResult<LootTableDto>> Get(string lootTableId)
    {
        var row = await WithChildren().FirstOrDefaultAsync(t => t.LootTableId == lootTableId);
        return row is null ? NotFound() : ToDto(row);
    }

    [HttpPost]
    public async Task<ActionResult<LootTableDto>> Create(LootTableDto input)
    {
        input.LootTableId = (input.LootTableId ?? "").Trim();
        if (string.IsNullOrEmpty(input.LootTableId))
            return BadRequest("loot_table_id is required.");
        if (await _db.LootTables.AnyAsync(t => t.LootTableId == input.LootTableId))
            return Conflict($"A loot table with id '{input.LootTableId}' already exists.");

        var row = new LootTable
        {
            LootTableId = input.LootTableId,
            DisplayName = input.DisplayName ?? "",
            UpdatedAt = DateTime.UtcNow,
        };
        Fill(row, input);
        _db.LootTables.Add(row);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { lootTableId = row.LootTableId }, ToDto(row));
    }

    [HttpPut("{lootTableId}")]
    public async Task<ActionResult<LootTableDto>> Update(string lootTableId, LootTableDto input)
    {
        var row = await WithChildren().FirstOrDefaultAsync(t => t.LootTableId == lootTableId);
        if (row is null) return NotFound();

        row.DisplayName = input.DisplayName ?? "";
        row.UpdatedAt = DateTime.UtcNow;
        input.LootTableId = lootTableId;
        row.Items.Clear();
        row.DropCounts.Clear();
        row.CoinTiers.Clear();
        Fill(row, input);

        await _db.SaveChangesAsync();
        return ToDto(row);
    }

    [HttpDelete("{lootTableId}")]
    public async Task<IActionResult> Delete(string lootTableId)
    {
        var row = await _db.LootTables.FindAsync(lootTableId);
        if (row is null) return NotFound();
        _db.LootTables.Remove(row); // child rows cascade (FK ON DELETE CASCADE)
        await _db.SaveChangesAsync();
        return NoContent();
    }

    static void Fill(LootTable row, LootTableDto input)
    {
        int i = 0;
        foreach (var it in input.Items ?? new List<LootItemDto>())
        {
            var id = (it.ItemId ?? "").Trim();
            if (string.IsNullOrEmpty(id)) continue;
            row.Items.Add(new LootItem { LootTableId = input.LootTableId, ItemId = id, Weight = it.Weight, SortOrder = i++ });
        }
        i = 0;
        foreach (var d in input.DropCounts ?? new List<LootDropCountDto>())
            row.DropCounts.Add(new LootDropCount { LootTableId = input.LootTableId, Count = d.Count, Weight = d.Weight, SortOrder = i++ });
        i = 0;
        foreach (var c in input.CoinTiers ?? new List<LootCoinTierDto>())
            row.CoinTiers.Add(new LootCoinTier { LootTableId = input.LootTableId, MinCopper = c.MinCopper, MaxCopper = c.MaxCopper, Weight = c.Weight, SortOrder = i++ });
    }
}
