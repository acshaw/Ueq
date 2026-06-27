using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Server-only entry point that loads DB-backed content into the in-memory registries at host start.
/// Each content type adds a step that reads its rows (via a repository) and pushes them into its
/// registry — swapping the *source* behind each registry's lookup-by-id API from <c>Resources.LoadAll</c>
/// to Postgres. Runs synchronously on the main thread right after <see cref="Database.InitializeServer"/>,
/// before the world goes live (same model as migrations; not on the gameplay tick). Throwing here
/// aborts server start (the caller wraps it) so a content/DB problem fails loudly.
///
/// The loaded snapshots are cached so the per-client catalog sync (<c>ContentCatalog</c>) can serve
/// clients without re-querying the DB.
/// </summary>
public static class ContentLoader
{
    /// <summary>Item snapshots loaded at startup — the source for both the server registry and the client catalog sync.</summary>
    public static IReadOnlyList<ItemSnapshot> Items { get; private set; } = new List<ItemSnapshot>();

    /// <summary>Load every DB-backed content type into its registry. Add a step per type as it migrates.</summary>
    public static void LoadAll()
    {
        using var conn = Database.OpenConnection();

        // ── Items (M2.2 — first real content type) ──────────────────────────────────────
        var items = new ItemRepository().LoadAll(conn);
        Items = items;
        if (ItemRegistry.Instance != null)
            ItemRegistry.Instance.LoadFrom(items);
        else
            Debug.LogWarning("[Content] ItemRegistry.Instance is null at load — items not registered on the server.");
        Debug.Log($"[Content] Loaded {items.Count} item(s) from the database.");

        // ── Vendor inventories (M2.3 — server-only, no client sync) ─────────────────────
        var vendors = new VendorInventoryRepository().LoadAll(conn);
        VendorRegistry.LoadFrom(vendors);
        Debug.Log($"[Content] Loaded {vendors.Count} vendor inventory(ies) from the database.");

        // ── Conversations (M2.4 — server-only) ──────────────────────────────────────────
        var conversations = new ConversationRepository().LoadAll(conn);
        ConversationRegistry.LoadFrom(conversations);
        Debug.Log($"[Content] Loaded {conversations.Count} conversation set(s) from the database.");

        // ── Factions (M2.6 — server-only; BEFORE mobs, which resolve def.faction at build) ──
        var factions = new FactionRepository().LoadAll(conn);
        FactionRegistry.LoadFrom(factions);
        Debug.Log($"[Content] Loaded {FactionRegistry.Count} faction(s) from the database.");

        // ── Loot tables (M2.7 — server-only; BEFORE mobs, which resolve def.lootTable; needs items) ──
        var lootTables = new LootRepository().LoadAll(conn);
        LootRegistry.LoadFrom(lootTables);
        Debug.Log($"[Content] Loaded {LootRegistry.Count} loot table(s) from the database.");

        // ── Mobs (M2.5 — server-only; after the content they reference) ──────────────────
        var mobs = new MobRepository().LoadAll(conn);
        MobRegistry.LoadFrom(mobs);
        Debug.Log($"[Content] Loaded {mobs.Count} mob(s) from the database.");

        // ── Spawn tables (M2.7.2 — server-only; AFTER mobs, whose ids the entries resolve) ──
        var spawnTables = new SpawnTableRepository().LoadAll(conn);
        SpawnTableRegistry.LoadFrom(spawnTables);
        Debug.Log($"[Content] Loaded {SpawnTableRegistry.Count} spawn table(s) from the database.");

        // ── XP curve (M2.7 — server-only; order-independent) ─────────────────────────────
        var xpTable = new XpRepository().LoadTable(conn);
        PlayerExperience.SetTable(xpTable);
        Debug.Log($"[Content] Loaded {(xpTable != null ? xpTable.Count : 0)} XP level(s) from the database.");
    }
}
