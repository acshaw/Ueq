using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Ueq/Loot Table")]
public class LootTable : ScriptableObject
{
    [System.Serializable]
    public struct ItemEntry
    {
        public ItemDefinition item;
        public int            weight;
    }

    [System.Serializable]
    public struct DropCountEntry
    {
        public int count;
        public int weight;
    }

    // minCopper == maxCopper for fixed amounts; spread the range for higher-tier mobs.
    [System.Serializable]
    public struct CoinTier
    {
        public int minCopper;
        public int maxCopper;
        public int weight;
    }

    [Header("Items")]
    public List<ItemEntry>      items      = new();
    public List<DropCountEntry> dropCounts = new();

    [Header("Coin")]
    public List<CoinTier> coinTiers = new();

    // Returns one InventorySlot per rolled drop (qty=1 each, unstacked).
    public void Roll(out List<InventorySlot> outSlots, out int outCopper)
    {
        outSlots = new List<InventorySlot>();

        int count = RollDropCount();
        for (int i = 0; i < count; i++)
        {
            string id = RollItemId();
            if (!string.IsNullOrEmpty(id))
                outSlots.Add(new InventorySlot { itemId = id, quantity = 1 });
        }

        outCopper = RollCoin();
    }

    // ── Internal rolls ────────────────────────────────────────────────────────

    int RollDropCount()
    {
        if (dropCounts == null || dropCounts.Count == 0) return 0;
        return WeightedPick(dropCounts, e => e.weight, e => e.count, 0);
    }

    string RollItemId()
    {
        if (items == null || items.Count == 0) return null;
        var entry = WeightedPick(items, e => e.weight, e => e, default);
        return entry.item != null ? entry.item.itemId : null;
    }

    int RollCoin()
    {
        if (coinTiers == null || coinTiers.Count == 0) return 0;
        var tier = WeightedPick(coinTiers, t => t.weight, t => t, default);
        return tier.minCopper >= tier.maxCopper
            ? tier.minCopper
            : Random.Range(tier.minCopper, tier.maxCopper + 1);
    }

    static TResult WeightedPick<T, TResult>(
        List<T> list,
        System.Func<T, int> weight,
        System.Func<T, TResult> value,
        TResult fallback)
    {
        int total = 0;
        foreach (var e in list) total += weight(e);
        if (total <= 0) return fallback;

        int roll = Random.Range(0, total);
        int acc  = 0;
        foreach (var e in list)
        {
            acc += weight(e);
            if (roll < acc) return value(e);
        }
        return fallback;
    }
}
