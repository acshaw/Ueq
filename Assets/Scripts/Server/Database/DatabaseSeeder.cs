using Npgsql;
using UnityEngine;

/// <summary>
/// Dev-only seed hook, run after migrations when seedOnStart is enabled.
/// Content/seed data (reference rows, dev fixtures) plugs in here as the schema grows.
/// </summary>
public static class DatabaseSeeder
{
    // Host-mode convenience account (1.4 / decision O5) — the dev login auto-fills these.
    public const string DevUsername = "dev";
    public const string DevPassword = "devpass";

    public static void Seed(NpgsqlConnection conn)
    {
        SeedDevAccount(conn);
        SeedStarterItems(conn);
        SeedAbilities(conn);
        SeedRaces(conn);
        SeedClasses(conn);   // after abilities — class_starting_abilities FKs to abilities
        SeedStarterVendors(conn);
        SeedConversations(conn);
        SeedFactions(conn);
        SeedLootTables(conn);
        SeedXpTable(conn);
        SeedMobs(conn);
        SeedMobFactionHits(conn);
        SeedSpawnTables(conn);
        SeedExampleEncounters(conn);      // 3.1.10 Stage 3 — running-start wilderness content
        SeedThornwoodEncounters(conn);    // 3.5 — Thornwood's goblin-patrol population
    }

    // ── Example wilderness encounters (3.1.10 Stage 3) ──────────────────────────────────────────
    // A running-start content set for Creslin's Field: 3 wilderness mob types, a weighted "random
    // encounter" table, and a Monster faction all player races are hostile to (so they aggro on sight).
    // Edit these in the web Mob/Spawn editors, or delete this method + its call to remove the examples.
    // The bodies are wired to Synty Dungeon prefabs by Tools/Zones/Build Example Encounters (editor side).
    static void SeedExampleEncounters(NpgsqlConnection conn)
    {
        // Faction: everything the players are hostile to. (NPC-to-NPC guard↔monster relation drives future
        // social aggro — harmless now.)
        SeedFaction(conn, "Monster", "Monsters");
        SeedRaceDefault(conn, "Human", "Monster", -2000);
        SeedRaceDefault(conn, "Dwarf", "Monster", -2000);
        SeedRaceDefault(conn, "Troll", "Monster", -2000);
        SeedRelation(conn, "CityGuards", "Monster", "hostile");
        SeedRelation(conn, "Monster", "CityGuards", "hostile");

        // 3 wilderness mob types — increasing level/HP/attack/XP. All wander, all Monster faction.
        // modelId resolves by convention (mob id → MobModelCatalog entry); Goblin Scout reuses the rat loot.
        SeedWildMob(conn, "Goblin Scout",     2, 25, 2,  50, loot: "Giant Rat Loot Table");
        SeedWildMob(conn, "Skeleton Soldier", 4, 45, 4,  90);
        SeedWildMob(conn, "Goblin Warchief",  6, 90, 7, 200);

        // Weighted "random encounter" table — mostly scouts, the warchief is rare. 45s ± 15s respawn.
        if (SeedSpawnTableHeader(conn, "Creslins Field Wildlife", "Creslins Field Wildlife", 45, 15))
        {
            SeedSpawnEntry(conn, "Creslins Field Wildlife", "Goblin Scout",     4, 0);
            SeedSpawnEntry(conn, "Creslins Field Wildlife", "Skeleton Soldier", 2, 1);
            SeedSpawnEntry(conn, "Creslins Field Wildlife", "Goblin Warchief",  1, 2);
            Debug.Log("[DB] Seed: example wildlife (Goblin Scout/Skeleton Soldier/Goblin Warchief + table).");
        }
    }

    static void SeedWildMob(NpgsqlConnection conn, string id, int level, int hp, int atk, int xp,
                            string loot = null)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO mobs (mob_id, display_name, prefab_address, mob_level, max_health, attack_damage, " +
            "movement_type, faction_id, loot_table_id, xp_reward) " +
            "VALUES (@id, @name, 'Enemy', @lvl, @hp, @atk, 1, 'Monster', @loot, @xp) " +
            "ON CONFLICT (mob_id) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", id);
        cmd.Parameters.AddWithValue("lvl", level);
        cmd.Parameters.AddWithValue("hp", hp);
        cmd.Parameters.AddWithValue("atk", atk);
        cmd.Parameters.AddWithValue("loot", (object)loot ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("xp", xp);
        cmd.ExecuteNonQuery();
    }

    // Returns true only on first insert (so entries are added once — later web edits are left intact).
    static bool SeedSpawnTableHeader(NpgsqlConnection conn, string id, string name, int baseSeconds, int variance)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO spawn_tables (spawn_table_id, display_name, timer_base_seconds, timer_variance) " +
            "VALUES (@id, @name, @base, @var) ON CONFLICT (spawn_table_id) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("base", baseSeconds);
        cmd.Parameters.AddWithValue("var", variance);
        return cmd.ExecuteNonQuery() > 0;
    }

    static void SeedSpawnEntry(NpgsqlConnection conn, string tableId, string mobId, int weight, int order, int groupSize = 1)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO spawn_table_entries (spawn_table_id, mob_id, weight, group_size, sort_order) " +
            "VALUES (@id, @mob, @w, @grp, @o)", conn);
        cmd.Parameters.AddWithValue("id", tableId);
        cmd.Parameters.AddWithValue("mob", mobId);
        cmd.Parameters.AddWithValue("w", weight);
        cmd.Parameters.AddWithValue("grp", groupSize);
        cmd.Parameters.AddWithValue("o", order);
        cmd.ExecuteNonQuery();
    }

    // ── Thornwood goblin patrols (3.5) — the second zone's signature encounter, per Zone 2 in
    // trellis_zone_design.md: mixed scouts moving in groups near the southern treeline, an organized
    // warrior patrol nearer the Grukmar's Deep entrance. Reuses "Goblin Scout" (shared with Creslin's
    // Field — raise its level to 6-9 for Thornwood in the web Mob Editor; seeding never overwrites that).
    static void SeedThornwoodEncounters(NpgsqlConnection conn)
    {
        SeedWildMob(conn, "Goblin Warrior", 7, 65, 6, 150);

        if (SeedSpawnTableHeader(conn, "Thornwood Goblin Patrol", "Thornwood Goblin Patrol", 60, 20))
        {
            SeedSpawnEntry(conn, "Thornwood Goblin Patrol", "Goblin Scout", 1, 0, groupSize: 3);
            Debug.Log("[DB] Seed: Thornwood Goblin Patrol (Goblin Scout, group of 3).");
        }
        if (SeedSpawnTableHeader(conn, "Thornwood Warrior Patrol", "Thornwood Warrior Patrol", 90, 20))
        {
            SeedSpawnEntry(conn, "Thornwood Warrior Patrol", "Goblin Warrior", 1, 0, groupSize: 2);
            Debug.Log("[DB] Seed: Thornwood Warrior Patrol (Goblin Warrior, group of 2).");
        }
    }

    // ── Spawn tables (M2.7.2) — migrate the existing SO spawn tables (all on the "Fast" 30s timer) ──
    static void SeedSpawnTables(NpgsqlConnection conn)
    {
        SeedSpawnTable(conn, "Mob Spawn Table",     "Mob Spawn Table",     "Giant Rat");
        SeedSpawnTable(conn, "Guard Spawn Table",   "Guard Spawn Table",   "City Guard");
        SeedSpawnTable(conn, "Captain Spawn Table", "Captain Spawn Table", "Captain of the Guard");
    }

    static void SeedSpawnTable(NpgsqlConnection conn, string id, string name, string mobId)
    {
        using (var ins = new NpgsqlCommand(
            "INSERT INTO spawn_tables (spawn_table_id, display_name, timer_base_seconds, timer_variance) " +
            "VALUES (@id, @name, 30, 0) ON CONFLICT (spawn_table_id) DO NOTHING", conn))
        {
            ins.Parameters.AddWithValue("id", id);
            ins.Parameters.AddWithValue("name", name);
            if (ins.ExecuteNonQuery() == 0) return;   // already seeded — leave web edits intact
        }

        using var entry = new NpgsqlCommand(
            "INSERT INTO spawn_table_entries (spawn_table_id, mob_id, weight, group_size, sort_order) " +
            "VALUES (@id, @mob, 1, 1, 0)", conn);
        entry.Parameters.AddWithValue("id", id);
        entry.Parameters.AddWithValue("mob", mobId);
        entry.ExecuteNonQuery();
    }

    // ── Mob faction hits on kill (M2.7.1) — demo placeholders so the loop is testable ──────────
    static void SeedMobFactionHits(NpgsqlConnection conn)
    {
        // Giant Rat: killing vermin lowers your Vermin standing, slightly raises CityGuards.
        SeedMobFactionHit(conn, "Giant Rat", "Vermin", -10, 0);
        SeedMobFactionHit(conn, "Giant Rat", "CityGuards", 2, 1);
        // Guards: killing them tanks your guard standing.
        SeedMobFactionHit(conn, "City Guard", "CityGuards", -50, 0);
        SeedMobFactionHit(conn, "Captain of the Guard", "CityGuards", -50, 0);
    }

    static void SeedMobFactionHit(NpgsqlConnection conn, string mobId, string factionId, int delta, int order)
    {
        // Idempotent: only insert if this mob has no hit for this faction yet (leave web edits intact).
        using var cmd = new NpgsqlCommand(
            "INSERT INTO mob_faction_hits (mob_id, faction_id, delta, sort_order) " +
            "SELECT @mob, @fac, @delta, @order " +
            "WHERE EXISTS (SELECT 1 FROM mobs WHERE mob_id = @mob) " +
            "AND NOT EXISTS (SELECT 1 FROM mob_faction_hits WHERE mob_id = @mob AND faction_id = @fac)", conn);
        cmd.Parameters.AddWithValue("mob", mobId);
        cmd.Parameters.AddWithValue("fac", factionId);
        cmd.Parameters.AddWithValue("delta", delta);
        cmd.Parameters.AddWithValue("order", order);
        cmd.ExecuteNonQuery();
    }

    // ── Loot tables (M2.7) — migrate the existing SO loot assets ────────────────────────────
    static void SeedLootTables(NpgsqlConnection conn)
    {
        // Giant Rat Loot Table (from the SO): 3 rat parts (weight 1 each), a drop-count curve, coin tiers.
        using (var ins = new NpgsqlCommand(
            "INSERT INTO loot_tables (loot_table_id, display_name) VALUES (@id, @name) " +
            "ON CONFLICT (loot_table_id) DO NOTHING", conn))
        {
            ins.Parameters.AddWithValue("id", "Giant Rat Loot Table");
            ins.Parameters.AddWithValue("name", "Giant Rat Loot Table");
            if (ins.ExecuteNonQuery() == 0) return;   // already seeded — leave web edits intact
        }

        const string lt = "Giant Rat Loot Table";
        SeedLootItem(conn, lt, "rat_paw",     1, 0);
        SeedLootItem(conn, lt, "rat_ear",     1, 1);
        SeedLootItem(conn, lt, "rat_whisker", 1, 2);

        SeedLootDropCount(conn, lt, 0, 50, 0);
        SeedLootDropCount(conn, lt, 1, 30, 1);
        SeedLootDropCount(conn, lt, 2, 15, 2);
        SeedLootDropCount(conn, lt, 3, 5,  3);

        SeedLootCoinTier(conn, lt, 0, 0, 21, 0);
        SeedLootCoinTier(conn, lt, 1, 1, 5,  1);
        SeedLootCoinTier(conn, lt, 2, 2, 4,  2);
        SeedLootCoinTier(conn, lt, 3, 3, 3,  3);
        SeedLootCoinTier(conn, lt, 4, 4, 2,  4);
        SeedLootCoinTier(conn, lt, 5, 5, 1,  5);
        Debug.Log("[DB] Seed: created Giant Rat Loot Table.");
    }

    static void SeedLootItem(NpgsqlConnection conn, string lt, string itemId, int weight, int order)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO loot_table_items (loot_table_id, item_id, weight, sort_order) " +
            "VALUES (@lt, @id, @w, @o)", conn);
        cmd.Parameters.AddWithValue("lt", lt);
        cmd.Parameters.AddWithValue("id", itemId);
        cmd.Parameters.AddWithValue("w", weight);
        cmd.Parameters.AddWithValue("o", order);
        cmd.ExecuteNonQuery();
    }

    static void SeedLootDropCount(NpgsqlConnection conn, string lt, int count, int weight, int order)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO loot_table_drop_counts (loot_table_id, count, weight, sort_order) " +
            "VALUES (@lt, @c, @w, @o)", conn);
        cmd.Parameters.AddWithValue("lt", lt);
        cmd.Parameters.AddWithValue("c", count);
        cmd.Parameters.AddWithValue("w", weight);
        cmd.Parameters.AddWithValue("o", order);
        cmd.ExecuteNonQuery();
    }

    static void SeedLootCoinTier(NpgsqlConnection conn, string lt, int min, int max, int weight, int order)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO loot_table_coin_tiers (loot_table_id, min_copper, max_copper, weight, sort_order) " +
            "VALUES (@lt, @min, @max, @w, @o)", conn);
        cmd.Parameters.AddWithValue("lt", lt);
        cmd.Parameters.AddWithValue("min", min);
        cmd.Parameters.AddWithValue("max", max);
        cmd.Parameters.AddWithValue("w", weight);
        cmd.Parameters.AddWithValue("o", order);
        cmd.ExecuteNonQuery();
    }

    // ── XP curve (M2.7) — migrate the 50-level table from XpTableDefinition.DefaultValues ────
    static void SeedXpTable(NpgsqlConnection conn)
    {
        var values = XpTableDefinition.DefaultValues;
        for (int i = 0; i < values.Length; i++)
        {
            using var cmd = new NpgsqlCommand(
                "INSERT INTO xp_levels (level, xp_to_next) VALUES (@lvl, @xp) " +
                "ON CONFLICT (level) DO NOTHING", conn);
            cmd.Parameters.AddWithValue("lvl", i + 1);
            cmd.Parameters.AddWithValue("xp", values[i]);
            cmd.ExecuteNonQuery();
        }
    }

    // ── Factions (M2.6) — migrate the existing SO faction assets + shared threshold ladder ──
    static void SeedFactions(NpgsqlConnection conn)
    {
        // Shared named-threshold ladder (from DefaultThresholds.asset). considerText = the 5.4 (AG1)
        // "consider" message, composed client-facing as "{target} {considerText}." — fully re-editable
        // per-faction afterward in the Faction Editor like every other seeded default here.
        var thresholds = new (string name, int min, int order, string considerText)[]
        {
            ("KOS", -10000, 0, "would attack you on sight"),
            ("Threatening", -750, 1, "glares at you with naked hostility"),
            ("Dubious", -500, 2, "eyes you with deep suspicion"),
            ("Apprehensive", -100, 3, "watches you warily"),
            ("Indifferent", 0, 4, "regards you indifferently"),
            ("Amiable", 100, 5, "looks upon you with mild favor"),
            ("Kindly", 500, 6, "smiles kindly at you"),
            ("Warmly", 750, 7, "greets you warmly"),
            ("Ally", 1100, 8, "looks upon you as a true and trusted ally"),
        };
        // ON CONFLICT DO UPDATE (backfill-if-blank), not DO NOTHING: a plain DO NOTHING would never
        // populate consider_text on a threshold row that already existed before 5.4 (i.e. any dev/prod
        // database seeded before this session) — the row "conflicts" on name and the whole insert, new
        // column included, is skipped. Only touches consider_text, and only when still blank, so a future
        // web-authored edit is never clobbered (same "web edits are never overwritten" rule this file's
        // header comment states for name/min_score elsewhere).
        foreach (var (name, min, order, considerText) in thresholds)
        {
            using var cmd = new NpgsqlCommand(
                "INSERT INTO faction_thresholds (name, min_score, sort_order, consider_text) " +
                "VALUES (@n, @m, @o, @c) " +
                "ON CONFLICT (name) DO UPDATE SET consider_text = @c " +
                "WHERE faction_thresholds.consider_text = ''", conn);
            cmd.Parameters.AddWithValue("n", name);
            cmd.Parameters.AddWithValue("m", min);
            cmd.Parameters.AddWithValue("o", order);
            cmd.Parameters.AddWithValue("c", considerText);
            cmd.ExecuteNonQuery();
        }

        // Factions (id, display name). faction_id matches the asset m_Name used by mob/conversation refs.
        SeedFaction(conn, "CityGuards",   "City Guards");
        SeedFaction(conn, "Vermin",       "Vermin");
        SeedFaction(conn, "QeynosGuards", "Qeynos Guards");

        // NPC-to-NPC relations — CityGuards ⇄ Vermin mutually hostile (faithful to the assets).
        SeedRelation(conn, "CityGuards", "Vermin", "hostile");
        SeedRelation(conn, "Vermin", "CityGuards", "hostile");

        // Race → faction starting scores (from RaceDefaults.asset).
        SeedRaceDefault(conn, "Human", "CityGuards", 0);
        SeedRaceDefault(conn, "Troll", "CityGuards", -5000);
        SeedRaceDefault(conn, "Dwarf", "CityGuards", 500);
        SeedRaceDefault(conn, "Human", "Vermin", -1000);
        SeedRaceDefault(conn, "Troll", "Vermin", -10000);
        SeedRaceDefault(conn, "Dwarf", "Vermin", -10000);
    }

    static void SeedFaction(NpgsqlConnection conn, string id, string name)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO factions (faction_id, faction_name) VALUES (@id, @name) " +
            "ON CONFLICT (faction_id) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", name);
        cmd.ExecuteNonQuery();
    }

    static void SeedRelation(NpgsqlConnection conn, string id, string other, string relation)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO faction_relations (faction_id, other_faction_id, relation) VALUES (@id, @o, @r) " +
            "ON CONFLICT (faction_id, other_faction_id, relation) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("o", other);
        cmd.Parameters.AddWithValue("r", relation);
        cmd.ExecuteNonQuery();
    }

    static void SeedRaceDefault(NpgsqlConnection conn, string race, string factionId, int score)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO race_faction_defaults (race, faction_id, score) VALUES (@race, @fid, @score) " +
            "ON CONFLICT (race, faction_id) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("race", race);
        cmd.Parameters.AddWithValue("fid", factionId);
        cmd.Parameters.AddWithValue("score", score);
        cmd.ExecuteNonQuery();
    }

    // ── Mobs (M2.5) — migrate the existing SO MobDefinitions ────────────────────────────────
    static void SeedMobs(NpgsqlConnection conn)
    {
        // (mob_id, display_name, prefab, max_health, faction_id, conversation_set_id, loot_table_id, vendor_id)
        // All other fields fall to the migration's column defaults (matching the original assets).
        SeedMob(conn, "Giant Rat", "Giant Rat", "Enemy", 10, faction: "Vermin",
                lootTable: "Giant Rat Loot Table");
        SeedMob(conn, "City Guard", "City Guard", "Enemy", 50, faction: "CityGuards");
        SeedMob(conn, "Captain of the Guard", "Captain of the Guard", "Enemy", 10, faction: "CityGuards",
                conversation: "Captain");
        // Merchant: original asset had NO prefab and NO conversation (test-data gaps) — seeded faithfully.
        // To make it a working shopkeeper, set a prefab + a conversation set with a "wares" keyword in the editor.
        SeedMob(conn, "Merchant", "Merchant", null, 10, vendor: "Merchant");
    }

    static void SeedMob(NpgsqlConnection conn, string id, string name, string prefab, int maxHealth,
                        string faction = null, string conversation = null, string lootTable = null,
                        string vendor = null)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO mobs (mob_id, display_name, prefab_address, max_health, faction_id, " +
            "conversation_set_id, loot_table_id, vendor_id) " +
            "VALUES (@id, @name, @prefab, @hp, @fac, @conv, @loot, @vendor) " +
            "ON CONFLICT (mob_id) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("prefab", (object)prefab ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("hp", maxHealth);
        cmd.Parameters.AddWithValue("fac", (object)faction ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("conv", (object)conversation ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("loot", (object)lootTable ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("vendor", (object)vendor ?? System.DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    // ── Vendors (M2.3) — migrate the existing Resources/Vendors/Merchant.asset ──────────────
    static void SeedStarterVendors(NpgsqlConnection conn)
    {
        // Merchant sold one item: Tunic. Idempotent — only stock it on first insert.
        using var ins = new NpgsqlCommand(
            "INSERT INTO vendor_inventories (vendor_id, display_name) VALUES ('Merchant', 'Merchant') " +
            "ON CONFLICT (vendor_id) DO NOTHING", conn);
        if (ins.ExecuteNonQuery() > 0)
        {
            using var item = new NpgsqlCommand(
                "INSERT INTO vendor_inventory_items (vendor_id, item_id, sort_order) " +
                "VALUES ('Merchant', 'tunic', 0)", conn);
            item.ExecuteNonQuery();
            Debug.Log("[DB] Seed: created Merchant vendor.");
        }
    }

    // ── Conversations (M2.4) — migrate the existing SO keyword sets ──────────────────────────
    static void SeedConversations(NpgsqlConnection conn)
    {
        SeedConvSet(conn, "Captain", "Captain", new[]
        {
            Kw("Hail", mode: 0, opener: true, ends: true, response: "Well met traveller!"),
        });

        SeedConvSet(conn, "GuardKeywords", "Guard Keywords", new[]
        {
            Kw("hail",     mode: 0, opener: true,
               response: "Hail, <name>! Well met. Ask me about [guards] or [patrol]."),
            Kw("guards",   mode: 0, response: "The guards are stationed at the north gate."),
            Kw("patrol",   mode: 1, response: "We patrol these roads to keep travelers like you safe, <race>.",
               unlocks: new[] { "danger" }),
            Kw("danger",   mode: 1, requiresUnlock: true,
               response: "Strange creatures have been seen to the east. Be wary."),
            Kw("help",     mode: 1, response: "What do you need, adventurer?",
               requiredFactionId: "CityGuards", requiredStanding: "Indifferent"),
            Kw("farewell", mode: 0, ends: true, response: "Safe travels, <name>."),
            // Vendor-open keyword: must NOT end the conversation, or VendorApplicator's shop
            // is closed in the same call (DispatchConversationEnd) right after it opens.
            Kw("wares",    mode: 0, response: "What would you like?"),
        });
    }

    struct SeedKw
    {
        public string Keyword, Response, RequiredFactionId, RequiredStanding;
        public int    Mode;
        public bool   Opener, Ends, RequiresUnlock;
        public string[] Unlocks;
    }

    static SeedKw Kw(string keyword, int mode, string response, bool opener = false, bool ends = false,
                     bool requiresUnlock = false, string requiredFactionId = null,
                     string requiredStanding = null, string[] unlocks = null)
        => new SeedKw
        {
            Keyword = keyword, Mode = mode, Response = response, Opener = opener, Ends = ends,
            RequiresUnlock = requiresUnlock, RequiredFactionId = requiredFactionId,
            RequiredStanding = requiredStanding, Unlocks = unlocks ?? System.Array.Empty<string>(),
        };

    static void SeedConvSet(NpgsqlConnection conn, string setId, string displayName, SeedKw[] keywords)
    {
        using (var ins = new NpgsqlCommand(
            "INSERT INTO conversation_sets (set_id, display_name) VALUES (@s, @n) " +
            "ON CONFLICT (set_id) DO NOTHING", conn))
        {
            ins.Parameters.AddWithValue("s", setId);
            ins.Parameters.AddWithValue("n", displayName);
            if (ins.ExecuteNonQuery() == 0) return; // already seeded — leave web edits intact
        }

        for (int i = 0; i < keywords.Length; i++)
        {
            var kw = keywords[i];
            long keywordId;
            using (var ins = new NpgsqlCommand(
                "INSERT INTO conversation_keywords (set_id, sort_order, keyword, mode, is_opener, " +
                "ends_conversation, requires_unlock, response, required_faction_id, required_standing) " +
                "VALUES (@set, @ord, @kw, @mode, @opener, @ends, @req, @resp, @fac, @stand) RETURNING id", conn))
            {
                ins.Parameters.AddWithValue("set", setId);
                ins.Parameters.AddWithValue("ord", i);
                ins.Parameters.AddWithValue("kw", kw.Keyword);
                ins.Parameters.AddWithValue("mode", kw.Mode);
                ins.Parameters.AddWithValue("opener", kw.Opener);
                ins.Parameters.AddWithValue("ends", kw.Ends);
                ins.Parameters.AddWithValue("req", kw.RequiresUnlock);
                ins.Parameters.AddWithValue("resp", kw.Response ?? "");
                ins.Parameters.AddWithValue("fac", (object)kw.RequiredFactionId ?? System.DBNull.Value);
                ins.Parameters.AddWithValue("stand", (object)kw.RequiredStanding ?? System.DBNull.Value);
                keywordId = (long)ins.ExecuteScalar();
            }

            foreach (var unlock in kw.Unlocks)
            {
                using var u = new NpgsqlCommand(
                    "INSERT INTO conversation_keyword_unlocks (keyword_id, unlocked_keyword) VALUES (@k, @u)", conn);
                u.Parameters.AddWithValue("k", keywordId);
                u.Parameters.AddWithValue("u", unlock);
                u.ExecuteNonQuery();
            }
        }
        Debug.Log($"[DB] Seed: created conversation set '{setId}' ({keywords.Length} keyword(s)).");
    }

    // Idempotent bootstrap of the items that used to live in Resources/Items (M2.2). Keeps the game
    // working immediately after the Resources path is retired — loot/vendor/equip reference these
    // item_ids. ON CONFLICT DO NOTHING so edits made in the web Item Editor are never overwritten.
    static void SeedStarterItems(NpgsqlConnection conn)
    {
        // (item_id, display_name, description, max_stack, equippable, equip_slot, buy, sell)
        SeedItem(conn, "tunic",       "Tunic",       "A tunic",      1,  true,  1, 10, 5);
        SeedItem(conn, "rat_paw",     "Rat Paw",     "A rat paw",     20, false, 11, 0, 0);
        SeedItem(conn, "rat_ear",     "Rat Ear",     "A rat ear",     20, false, 11, 0, 0);
        SeedItem(conn, "rat_whisker", "Rat Whisker", "A rat whisker", 20, false, 11, 0, 0);
    }

    static void SeedItem(NpgsqlConnection conn, string id, string name, string desc,
                         int maxStack, bool equippable, int equipSlot, int buy, int sell)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO items (item_id, display_name, description, max_stack_size, " +
            "is_equippable, equip_slot, buy_price, sell_price) " +
            "VALUES (@id, @name, @desc, @stack, @equip, @slot, @buy, @sell) " +
            "ON CONFLICT (item_id) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("desc", desc);
        cmd.Parameters.AddWithValue("stack", maxStack);
        cmd.Parameters.AddWithValue("equip", equippable);
        cmd.Parameters.AddWithValue("slot", equipSlot);
        cmd.Parameters.AddWithValue("buy", buy);
        cmd.Parameters.AddWithValue("sell", sell);
        cmd.ExecuteNonQuery();
    }

    // Idempotent bootstrap of the abilities that used to live as Resources/Abilities SO assets (M2.9).
    // Faithfully migrated from those assets' YAML (exact ids preserved — existing character hotbars
    // already reference them). ON CONFLICT DO NOTHING on the header so web edits are never overwritten;
    // children only insert the first time a given ability's header row is newly created.
    static void SeedAbilities(NpgsqlConnection conn)
    {
        SeedAbilityTag(conn, "martialability", "MartialAbility");

        if (SeedAbility(conn, "kick", "Kick", "Kicks the target", targetingType: 1, range: 10, manaCost: 0, animTrigger: "Kick"))
        {
            SeedAbilityTagRef(conn, "kick", "martialability", 0);
            SeedAbilityCooldownLink(conn, "kick", "martialability", 10f, 0);
            SeedAbilityEffect(conn, "kick", "damage", 8, (int)ScalingStatType.Str, 0.5f, 0);
        }

        if (SeedAbility(conn, "Taunt", "Taunt", "Taunts the eneimy", targetingType: 1, range: 20, manaCost: 0, animTrigger: ""))
        {
            SeedAbilityTagRef(conn, "Taunt", "martialability", 0);
            SeedAbilityCooldownLink(conn, "Taunt", "martialability", 20f, 0);
        }
        // 5.4 (AG5) — Taunt originally shipped with zero effects (a real hotbar slot that mechanically did
        // nothing). Backfilled OUTSIDE the header-newly-created gate above, since Taunt already exists in
        // any database seeded before this session — ability_effects has no natural uniqueness constraint to
        // lean an ON CONFLICT clause on, so this is guarded by its own explicit existence check instead.
        if (!SeedAbilityEffectExists(conn, "Taunt", "threat"))
            SeedAbilityEffect(conn, "Taunt", "threat", 50, (int)ScalingStatType.None, 0f, 0);

        if (SeedAbility(conn, "fire_bolt", "Fire Bolt", "A bolt of fire that scorches a single enemy.",
                targetingType: 1, range: 20, manaCost: 10, animTrigger: "Cast"))
            SeedAbilityEffect(conn, "fire_bolt", "damage", 12, (int)ScalingStatType.Int, 0.5f, 0);

        // 5.4 follow-up: SingleTarget (was Self) so it can heal a targeted group member, not just yourself
        // — self-heal now goes through F1 (target self) like everything else instead of being implicit.
        // Fresh-install default only: an existing "minor_heal" row from before this change needs a one-
        // time edit via the web Ability Editor (Targeting → SingleTarget, Range → 20) — SeedAbility's
        // ON CONFLICT DO NOTHING never touches an existing header row, and unlike the consider-text/Taunt
        // backfills this is a genuine content change to something already fully editable there, not a
        // "born with missing data" gap worth a seeder-side special case.
        if (SeedAbility(conn, "minor_heal", "Minor Heal", "Channels a mending light, restoring health.",
                targetingType: 1, range: 20, manaCost: 10, animTrigger: "Cast"))
            SeedAbilityEffect(conn, "minor_heal", "heal", 20, (int)ScalingStatType.Wis, 0.5f, 0);

        Debug.Log("[DB] Seed: abilities ready (kick, Taunt, fire_bolt, minor_heal).");
    }

    static bool SeedAbility(NpgsqlConnection conn, string id, string name, string desc,
                            int targetingType, float range, int manaCost, string animTrigger)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO abilities (ability_id, display_name, description, targeting_type, range, mana_cost, anim_trigger) " +
            "VALUES (@id, @name, @desc, @tt, @range, @mana, @anim) " +
            "ON CONFLICT (ability_id) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("desc", desc);
        cmd.Parameters.AddWithValue("tt", targetingType);
        cmd.Parameters.AddWithValue("range", range);
        cmd.Parameters.AddWithValue("mana", manaCost);
        cmd.Parameters.AddWithValue("anim", animTrigger);
        return cmd.ExecuteNonQuery() > 0;
    }

    static void SeedAbilityTag(NpgsqlConnection conn, string tagId, string displayName)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO ability_tags (tag_id, display_name) VALUES (@id, @name) " +
            "ON CONFLICT (tag_id) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("id", tagId);
        cmd.Parameters.AddWithValue("name", displayName);
        cmd.ExecuteNonQuery();
    }

    static void SeedAbilityTagRef(NpgsqlConnection conn, string abilityId, string tagId, int order)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO ability_definition_tags (ability_id, tag_id, sort_order) VALUES (@a, @t, @o)", conn);
        cmd.Parameters.AddWithValue("a", abilityId);
        cmd.Parameters.AddWithValue("t", tagId);
        cmd.Parameters.AddWithValue("o", order);
        cmd.ExecuteNonQuery();
    }

    static void SeedAbilityCooldownLink(NpgsqlConnection conn, string abilityId, string tagId, float duration, int order)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO ability_cooldown_links (ability_id, tag_id, duration, sort_order) VALUES (@a, @t, @d, @o)", conn);
        cmd.Parameters.AddWithValue("a", abilityId);
        cmd.Parameters.AddWithValue("t", tagId);
        cmd.Parameters.AddWithValue("d", duration);
        cmd.Parameters.AddWithValue("o", order);
        cmd.ExecuteNonQuery();
    }

    // 5.4 (AG5) — existence check backing the Taunt effect backfill above (ability_effects has no unique
    // constraint on (ability_id, effect_type) to lean an ON CONFLICT clause on).
    static bool SeedAbilityEffectExists(NpgsqlConnection conn, string abilityId, string effectType)
    {
        using var cmd = new NpgsqlCommand(
            "SELECT 1 FROM ability_effects WHERE ability_id = @a AND effect_type = @t LIMIT 1", conn);
        cmd.Parameters.AddWithValue("a", abilityId);
        cmd.Parameters.AddWithValue("t", effectType);
        using var reader = cmd.ExecuteReader();
        return reader.Read();
    }

    static void SeedAbilityEffect(NpgsqlConnection conn, string abilityId, string effectType,
                                  int baseAmount, int scalingStat, float scalingFactor, int order)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO ability_effects (ability_id, effect_type, base_amount, scaling_stat, scaling_factor, sort_order) " +
            "VALUES (@a, @type, @amt, @stat, @factor, @o)", conn);
        cmd.Parameters.AddWithValue("a", abilityId);
        cmd.Parameters.AddWithValue("type", effectType);
        cmd.Parameters.AddWithValue("amt", baseAmount);
        cmd.Parameters.AddWithValue("stat", scalingStat);
        cmd.Parameters.AddWithValue("factor", scalingFactor);
        cmd.Parameters.AddWithValue("o", order);
        cmd.ExecuteNonQuery();
    }

    // Idempotent bootstrap of the races that used to live as Resources/Races SO assets (M2.10). Faithfully
    // migrated from those assets' YAML — exact values preserved. ON CONFLICT DO NOTHING so web edits stick.
    static void SeedRaces(NpgsqlConnection conn)
    {
        SeedRace(conn, "Human", "Human", 1f, 0, 0, 0, 0, 0, 0, 0);
        SeedRace(conn, "Dwarf", "Dwarf", 1f, 2, 3, -2, 0, -2, 1, 0);
        Debug.Log("[DB] Seed: races ready (Human, Dwarf).");
    }

    static void SeedRace(NpgsqlConnection conn, string id, string name, float xpMod,
                         int str, int sta, int agi, int dex, int intl, int wis, int cha)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO races (race_id, race_name, xp_modifier, str_mod, sta_mod, agi_mod, dex_mod, int_mod, wis_mod, cha_mod) " +
            "VALUES (@id, @name, @xp, @str, @sta, @agi, @dex, @int, @wis, @cha) " +
            "ON CONFLICT (race_id) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("xp", xpMod);
        cmd.Parameters.AddWithValue("str", str);
        cmd.Parameters.AddWithValue("sta", sta);
        cmd.Parameters.AddWithValue("agi", agi);
        cmd.Parameters.AddWithValue("dex", dex);
        cmd.Parameters.AddWithValue("int", intl);
        cmd.Parameters.AddWithValue("wis", wis);
        cmd.Parameters.AddWithValue("cha", cha);
        cmd.ExecuteNonQuery();
    }

    // Idempotent bootstrap of the classes that used to live as Resources/Classes SO assets (M2.10).
    // Faithfully migrated — exact values preserved (incl. Warrior's base stats all sitting at 75, notably
    // different from Wizard/Cleric's ~10 baseline; not "fixed" here, this is a pure data migration).
    // Requires SeedAbilities to have already run (class_starting_abilities FKs to abilities).
    static void SeedClasses(NpgsqlConnection conn)
    {
        if (SeedClass(conn, "Warrior", "Warrior", 1f,
                75, 75, 75, 75, 75, 75, 75,
                15, 4, 255, 0.23f, 0.15f,
                0, 0, 0, 0, 0.23f, 0f,
                17.5f, 40f, 30f, 10f, 2.5f, 0f, 0f,
                2f, 13f, 20f, 35f, 25f, 3f, 2f))
        {
            SeedClassStartingAbility(conn, "Warrior", "kick", 0);
            SeedClassStartingAbility(conn, "Warrior", "Taunt", 1);
        }

        if (SeedClass(conn, "Wizard", "Wizard", 1f,
                10, 10, 10, 10, 14, 10, 10,
                12, 1, 100, 0.23f, 0.12f,
                1, 40, 5, 200, 0.23f, 0.18f,
                25f, 40f, 25f, 7.5f, 2.5f, 0f, 0f,
                15.53f, 23.5f, 18.89f, 22.78f, 16.25f, 1.83f, 1.22f))
            SeedClassStartingAbility(conn, "Wizard", "fire_bolt", 0);

        if (SeedClass(conn, "Cleric", "Cleric", 1f,
                10, 10, 10, 10, 10, 14, 10,
                13, 2, 140, 0.23f, 0.12f,
                2, 35, 4, 200, 0.23f, 0.16f,
                20f, 40f, 30f, 7.5f, 2.5f, 0f, 0f,
                7.08f, 17.5f, 21.67f, 28.33f, 21.25f, 2.5f, 1.67f))
            SeedClassStartingAbility(conn, "Cleric", "minor_heal", 0);

        Debug.Log("[DB] Seed: classes ready (Warrior, Wizard, Cleric).");
    }

    static bool SeedClass(NpgsqlConnection conn, string id, string name, float xpMod,
        int str, int sta, int agi, int dex, int intl, int wis, int cha,
        int baseHp, int hpPerLevel, int staCap, float baseStaRatio, float staGrowthRate,
        int manaStatType, int baseMana, int manaPerLevel, int manaCap, float baseManaRatio, float manaGrowthRate,
        float l1Miss, float l1Glancing, float l1Hit, float l1Solid, float l1Good, float l1Critical, float l1Crippling,
        float l20Miss, float l20Glancing, float l20Hit, float l20Solid, float l20Good, float l20Critical, float l20Crippling)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO classes (class_id, class_name, xp_modifier, " +
            "base_str, base_sta, base_agi, base_dex, base_int, base_wis, base_cha, " +
            "class_base_hp, hp_per_level, sta_cap, base_sta_ratio, sta_growth_rate, " +
            "mana_stat_type, class_base_mana, mana_per_level, mana_cap, base_mana_ratio, mana_growth_rate, " +
            "tier_l1_miss, tier_l1_glancing, tier_l1_hit, tier_l1_solid, tier_l1_good, tier_l1_critical, tier_l1_crippling, " +
            "tier_l20_miss, tier_l20_glancing, tier_l20_hit, tier_l20_solid, tier_l20_good, tier_l20_critical, tier_l20_crippling) " +
            "VALUES (@id, @name, @xp, @str, @sta, @agi, @dex, @int, @wis, @cha, " +
            "@bhp, @hpl, @stacap, @bstaratio, @stagrow, " +
            "@mst, @bmana, @manal, @manacap, @bmanaratio, @managrow, " +
            "@l1mi, @l1gl, @l1hi, @l1so, @l1go, @l1cr, @l1cp, " +
            "@l20mi, @l20gl, @l20hi, @l20so, @l20go, @l20cr, @l20cp) " +
            "ON CONFLICT (class_id) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("xp", xpMod);
        cmd.Parameters.AddWithValue("str", str);
        cmd.Parameters.AddWithValue("sta", sta);
        cmd.Parameters.AddWithValue("agi", agi);
        cmd.Parameters.AddWithValue("dex", dex);
        cmd.Parameters.AddWithValue("int", intl);
        cmd.Parameters.AddWithValue("wis", wis);
        cmd.Parameters.AddWithValue("cha", cha);
        cmd.Parameters.AddWithValue("bhp", baseHp);
        cmd.Parameters.AddWithValue("hpl", hpPerLevel);
        cmd.Parameters.AddWithValue("stacap", staCap);
        cmd.Parameters.AddWithValue("bstaratio", baseStaRatio);
        cmd.Parameters.AddWithValue("stagrow", staGrowthRate);
        cmd.Parameters.AddWithValue("mst", manaStatType);
        cmd.Parameters.AddWithValue("bmana", baseMana);
        cmd.Parameters.AddWithValue("manal", manaPerLevel);
        cmd.Parameters.AddWithValue("manacap", manaCap);
        cmd.Parameters.AddWithValue("bmanaratio", baseManaRatio);
        cmd.Parameters.AddWithValue("managrow", manaGrowthRate);
        cmd.Parameters.AddWithValue("l1mi", l1Miss);
        cmd.Parameters.AddWithValue("l1gl", l1Glancing);
        cmd.Parameters.AddWithValue("l1hi", l1Hit);
        cmd.Parameters.AddWithValue("l1so", l1Solid);
        cmd.Parameters.AddWithValue("l1go", l1Good);
        cmd.Parameters.AddWithValue("l1cr", l1Critical);
        cmd.Parameters.AddWithValue("l1cp", l1Crippling);
        cmd.Parameters.AddWithValue("l20mi", l20Miss);
        cmd.Parameters.AddWithValue("l20gl", l20Glancing);
        cmd.Parameters.AddWithValue("l20hi", l20Hit);
        cmd.Parameters.AddWithValue("l20so", l20Solid);
        cmd.Parameters.AddWithValue("l20go", l20Good);
        cmd.Parameters.AddWithValue("l20cr", l20Critical);
        cmd.Parameters.AddWithValue("l20cp", l20Crippling);
        return cmd.ExecuteNonQuery() > 0;
    }

    static void SeedClassStartingAbility(NpgsqlConnection conn, string classId, string abilityId, int order)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO class_starting_abilities (class_id, ability_id, sort_order) VALUES (@c, @a, @o)", conn);
        cmd.Parameters.AddWithValue("c", classId);
        cmd.Parameters.AddWithValue("a", abilityId);
        cmd.Parameters.AddWithValue("o", order);
        cmd.ExecuteNonQuery();
    }

    // Idempotent: creates the dev account once so Host testing is one click.
    static void SeedDevAccount(NpgsqlConnection conn)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO accounts (username, password_hash) VALUES (@u, @h) " +
            "ON CONFLICT (username) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("u", DevUsername);
        cmd.Parameters.AddWithValue("h", PasswordHasher.Hash(DevPassword));
        int rows = cmd.ExecuteNonQuery();
        Debug.Log(rows > 0
            ? $"[DB] Seed: created dev account '{DevUsername}'."
            : "[DB] Seed: dev account already present.");
    }
}
