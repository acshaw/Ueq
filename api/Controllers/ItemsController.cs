using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// CRUD over the <c>items</c> table (M2.2) — the reference content controller every later type copies.
/// Keyed by the string <c>item_id</c> (the cross-system item key). Thin data pipe for the Angular
/// editor; no game logic, no auth (trusted local users, devplan D2).
/// </summary>
[ApiController]
[Route("api/items")]
public class ItemsController : ControllerBase
{
    readonly ContentDbContext _db;

    public ItemsController(ContentDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Item>>> GetAll() =>
        await _db.Items.OrderBy(i => i.ItemId).ToListAsync();

    [HttpGet("{itemId}")]
    public async Task<ActionResult<Item>> Get(string itemId)
    {
        var row = await _db.Items.FindAsync(itemId);
        return row is null ? NotFound() : row;
    }

    [HttpPost]
    public async Task<ActionResult<Item>> Create(Item input)
    {
        input.ItemId = (input.ItemId ?? "").Trim();
        if (string.IsNullOrEmpty(input.ItemId))
            return BadRequest("item_id is required.");
        if (await _db.Items.AnyAsync(i => i.ItemId == input.ItemId))
            return Conflict($"An item with id '{input.ItemId}' already exists.");

        input.UpdatedAt = DateTime.UtcNow;
        _db.Items.Add(input);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { itemId = input.ItemId }, input);
    }

    [HttpPut("{itemId}")]
    public async Task<ActionResult<Item>> Update(string itemId, Item input)
    {
        var row = await _db.Items.FindAsync(itemId);
        if (row is null) return NotFound();

        // item_id is the immutable key — copy everything else.
        row.DisplayName = input.DisplayName;
        row.Description = input.Description;
        row.MaxStackSize = input.MaxStackSize;
        row.IsEquippable = input.IsEquippable;
        row.EquipSlot = input.EquipSlot;
        row.BonusStr = input.BonusStr;
        row.BonusSta = input.BonusSta;
        row.BonusAgi = input.BonusAgi;
        row.BonusDex = input.BonusDex;
        row.BonusInt = input.BonusInt;
        row.BonusWis = input.BonusWis;
        row.BonusCha = input.BonusCha;
        row.BonusAc = input.BonusAc;
        row.WeaponBaseDamage = input.WeaponBaseDamage;
        row.WeaponBonusDamage = input.WeaponBonusDamage;
        row.WeaponDelay = input.WeaponDelay;
        row.WeaponRange = input.WeaponRange;
        row.WeaponCategory = input.WeaponCategory;
        row.BuyPrice = input.BuyPrice;
        row.SellPrice = input.SellPrice;
        row.Lore = input.Lore;
        row.IconAddress = string.IsNullOrWhiteSpace(input.IconAddress) ? null : input.IconAddress.Trim();
        row.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return row;
    }

    [HttpDelete("{itemId}")]
    public async Task<IActionResult> Delete(string itemId)
    {
        var row = await _db.Items.FindAsync(itemId);
        if (row is null) return NotFound();
        _db.Items.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
