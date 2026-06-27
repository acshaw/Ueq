namespace Ueq.ContentApi.Models;

/// <summary>EF entity for <c>vendor_inventories</c> (M2.3). Mapping-only; SQL runner owns the schema.</summary>
public class VendorInventory
{
    public string VendorId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

    public List<VendorInventoryItem> Items { get; set; } = new();
}

/// <summary>EF entity for <c>vendor_inventory_items</c> — one item the vendor sells, ordered.</summary>
public class VendorInventoryItem
{
    public long Id { get; set; }
    public string VendorId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>Flat shape the Angular editor works with — a vendor plus its ordered item ids.</summary>
public class VendorDto
{
    public string VendorId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> ItemIds { get; set; } = new();
}
