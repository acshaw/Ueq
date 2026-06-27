using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Data;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Controllers;

/// <summary>
/// CRUD over vendor inventories (M2.3). Exposes the flat <see cref="VendorDto"/> (vendor + ordered item
/// ids) the Angular editor works with; the child rows are managed internally. Keyed by string vendor_id.
/// </summary>
[ApiController]
[Route("api/vendors")]
public class VendorsController : ControllerBase
{
    readonly ContentDbContext _db;

    public VendorsController(ContentDbContext db) => _db = db;

    static VendorDto ToDto(VendorInventory v) => new()
    {
        VendorId = v.VendorId,
        DisplayName = v.DisplayName,
        ItemIds = v.Items.OrderBy(i => i.SortOrder).Select(i => i.ItemId).ToList(),
    };

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VendorDto>>> GetAll()
    {
        var rows = await _db.VendorInventories.Include(v => v.Items).OrderBy(v => v.VendorId).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    [HttpGet("{vendorId}")]
    public async Task<ActionResult<VendorDto>> Get(string vendorId)
    {
        var row = await _db.VendorInventories.Include(v => v.Items).FirstOrDefaultAsync(v => v.VendorId == vendorId);
        return row is null ? NotFound() : ToDto(row);
    }

    [HttpPost]
    public async Task<ActionResult<VendorDto>> Create(VendorDto input)
    {
        input.VendorId = (input.VendorId ?? "").Trim();
        if (string.IsNullOrEmpty(input.VendorId))
            return BadRequest("vendor_id is required.");
        if (await _db.VendorInventories.AnyAsync(v => v.VendorId == input.VendorId))
            return Conflict($"A vendor with id '{input.VendorId}' already exists.");

        var row = new VendorInventory
        {
            VendorId = input.VendorId,
            DisplayName = input.DisplayName ?? "",
            UpdatedAt = DateTime.UtcNow,
            Items = BuildItems(input),
        };
        _db.VendorInventories.Add(row);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { vendorId = row.VendorId }, ToDto(row));
    }

    [HttpPut("{vendorId}")]
    public async Task<ActionResult<VendorDto>> Update(string vendorId, VendorDto input)
    {
        var row = await _db.VendorInventories.Include(v => v.Items).FirstOrDefaultAsync(v => v.VendorId == vendorId);
        if (row is null) return NotFound();

        row.DisplayName = input.DisplayName ?? "";
        row.UpdatedAt = DateTime.UtcNow;
        // Replace the stock list wholesale (simplest correct semantics for an ordered list).
        row.Items.Clear();
        input.VendorId = vendorId;
        foreach (var it in BuildItems(input)) row.Items.Add(it);

        await _db.SaveChangesAsync();
        return ToDto(row);
    }

    [HttpDelete("{vendorId}")]
    public async Task<IActionResult> Delete(string vendorId)
    {
        var row = await _db.VendorInventories.FindAsync(vendorId);
        if (row is null) return NotFound();
        _db.VendorInventories.Remove(row); // child rows cascade (FK ON DELETE CASCADE)
        await _db.SaveChangesAsync();
        return NoContent();
    }

    static List<VendorInventoryItem> BuildItems(VendorDto input)
    {
        var items = new List<VendorInventoryItem>();
        var ids = input.ItemIds ?? new List<string>();
        for (int i = 0; i < ids.Count; i++)
        {
            var id = (ids[i] ?? "").Trim();
            if (string.IsNullOrEmpty(id)) continue;
            items.Add(new VendorInventoryItem { VendorId = input.VendorId, ItemId = id, SortOrder = i });
        }
        return items;
    }
}
