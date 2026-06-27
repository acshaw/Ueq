using System.Collections.Generic;
using Npgsql;

/// <summary>Plain-data view of one vendor inventory (M2.3) — an ordered list of item ids it sells.</summary>
public struct VendorInventorySnapshot
{
    public string       VendorId;
    public string       DisplayName;
    public List<string> ItemIds;
}

/// <summary>
/// Read-only repository over <c>vendor_inventories</c> (+ items child), M2.3. Server-only (vendor data
/// never leaves the server except the momentary stock list pushed on shop-open). 1.2 DAL convention.
/// </summary>
public sealed class VendorInventoryRepository : IRepository
{
    public List<VendorInventorySnapshot> LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        // Build vendor headers first, then attach items, so vendors with no items still load.
        var byId = new Dictionary<string, VendorInventorySnapshot>();
        var order = new List<string>();

        using (var cmd = new NpgsqlCommand(
            "SELECT vendor_id, display_name FROM vendor_inventories ORDER BY vendor_id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetString(0);
                byId[id] = new VendorInventorySnapshot
                {
                    VendorId    = id,
                    DisplayName = reader.GetString(1),
                    ItemIds     = new List<string>(),
                };
                order.Add(id);
            }
        }

        using (var cmd = new NpgsqlCommand(
            "SELECT vendor_id, item_id FROM vendor_inventory_items ORDER BY vendor_id, sort_order, id",
            conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var vid = reader.GetString(0);
                if (byId.TryGetValue(vid, out var snap))
                    snap.ItemIds.Add(reader.GetString(1));
            }
        }

        var rows = new List<VendorInventorySnapshot>(order.Count);
        foreach (var id in order) rows.Add(byId[id]);
        return rows;
    }
}
