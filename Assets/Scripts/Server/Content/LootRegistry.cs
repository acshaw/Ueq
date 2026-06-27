using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Server-only lookup of loot tables by id (M2.7). Builds a runtime <see cref="LootTable"/> per DB row —
/// the same type the SO path used — so <c>Corpse</c> (which already rolls <c>mob.lootTable</c>) is
/// unchanged; only the source moved off ScriptableObjects. Item entries resolve their id → the runtime
/// <c>ItemDefinition</c> via <see cref="ItemRegistry"/> (so items reach loot through the 2.2 registry, no
/// SO refs). No client sync. Populated by <c>ContentLoader</c> <b>before mobs</b> (mobs resolve their loot).
/// </summary>
public static class LootRegistry
{
    static readonly Dictionary<string, LootTable> _byId = new();

    public static void LoadFrom(IEnumerable<LootTableSnapshot> snapshots)
    {
        _byId.Clear();
        foreach (var s in snapshots)
            if (!string.IsNullOrEmpty(s.LootTableId))
                _byId[s.LootTableId] = Build(s);
    }

    public static LootTable Get(string lootTableId)
        => string.IsNullOrEmpty(lootTableId) ? null : _byId.GetValueOrDefault(lootTableId);

    public static int Count => _byId.Count;

    static LootTable Build(LootTableSnapshot s)
    {
        var table = ScriptableObject.CreateInstance<LootTable>();
        table.name = s.LootTableId;

        table.items = new List<LootTable.ItemEntry>();
        foreach (var i in s.Items)
        {
            var def = ItemRegistry.Instance != null ? ItemRegistry.Instance.Get(i.ItemId) : null;
            if (def == null)
            {
                Debug.LogWarning($"[Content] Loot table '{s.LootTableId}' references item '{i.ItemId}' " +
                                 "which is not in the item registry — that drop is skipped.");
                continue;
            }
            table.items.Add(new LootTable.ItemEntry { item = def, weight = i.Weight });
        }

        table.dropCounts = new List<LootTable.DropCountEntry>();
        foreach (var d in s.DropCounts)
            table.dropCounts.Add(new LootTable.DropCountEntry { count = d.Count, weight = d.Weight });

        table.coinTiers = new List<LootTable.CoinTier>();
        foreach (var c in s.CoinTiers)
            table.coinTiers.Add(new LootTable.CoinTier { minCopper = c.MinCopper, maxCopper = c.MaxCopper, weight = c.Weight });

        return table;
    }
}
