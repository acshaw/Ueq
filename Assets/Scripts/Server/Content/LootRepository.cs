using System.Collections.Generic;
using Npgsql;

/// <summary>Plain-data view of one loot table (M2.7) — weighted item pool + drop counts + coin tiers.</summary>
public struct LootTableSnapshot
{
    public string                    LootTableId;
    public string                    DisplayName;
    public List<LootItemSnapshot>    Items;
    public List<LootDropCountSnapshot> DropCounts;
    public List<LootCoinTierSnapshot> CoinTiers;
}

public struct LootItemSnapshot      { public string ItemId; public int Weight; }
public struct LootDropCountSnapshot { public int Count;  public int Weight; }
public struct LootCoinTierSnapshot  { public int MinCopper; public int MaxCopper; public int Weight; }

/// <summary>
/// Read-only repository over <c>loot_tables</c> (+ items / drop_counts / coin_tiers), M2.7. Server-only
/// (loot is rolled on the server when a mob dies). Header-then-children load (1.2 DAL convention); the
/// child lists are reference types shared with the snapshot copy placed in the result.
/// </summary>
public sealed class LootRepository : IRepository
{
    public List<LootTableSnapshot> LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var byId  = new Dictionary<string, LootTableSnapshot>();
        var order = new List<string>();

        using (var cmd = new NpgsqlCommand(
            "SELECT loot_table_id, display_name FROM loot_tables ORDER BY loot_table_id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetString(0);
                byId[id] = new LootTableSnapshot
                {
                    LootTableId = id,
                    DisplayName = reader.GetString(1),
                    Items       = new List<LootItemSnapshot>(),
                    DropCounts  = new List<LootDropCountSnapshot>(),
                    CoinTiers   = new List<LootCoinTierSnapshot>(),
                };
                order.Add(id);
            }
        }

        using (var cmd = new NpgsqlCommand(
            "SELECT loot_table_id, item_id, weight FROM loot_table_items ORDER BY loot_table_id, sort_order, id",
            conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                if (byId.TryGetValue(reader.GetString(0), out var s))
                    s.Items.Add(new LootItemSnapshot { ItemId = reader.GetString(1), Weight = reader.GetInt32(2) });
        }

        using (var cmd = new NpgsqlCommand(
            "SELECT loot_table_id, count, weight FROM loot_table_drop_counts ORDER BY loot_table_id, sort_order, id",
            conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                if (byId.TryGetValue(reader.GetString(0), out var s))
                    s.DropCounts.Add(new LootDropCountSnapshot { Count = reader.GetInt32(1), Weight = reader.GetInt32(2) });
        }

        using (var cmd = new NpgsqlCommand(
            "SELECT loot_table_id, min_copper, max_copper, weight FROM loot_table_coin_tiers " +
            "ORDER BY loot_table_id, sort_order, id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                if (byId.TryGetValue(reader.GetString(0), out var s))
                    s.CoinTiers.Add(new LootCoinTierSnapshot
                    {
                        MinCopper = reader.GetInt32(1), MaxCopper = reader.GetInt32(2), Weight = reader.GetInt32(3),
                    });
        }

        var rows = new List<LootTableSnapshot>(order.Count);
        foreach (var id in order) rows.Add(byId[id]);
        return rows;
    }
}
