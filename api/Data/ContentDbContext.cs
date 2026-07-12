using Microsoft.EntityFrameworkCore;
using Ueq.ContentApi.Models;

namespace Ueq.ContentApi.Data;

/// <summary>
/// EF Core context for content tables — <b>mapping-only</b>. It maps entities onto tables that
/// already exist (created by Unity's StreamingAssets <c>.sql</c> migration runner). EF Migrations
/// is never enabled and the app never calls <c>Migrate()</c>/<c>EnsureCreated()</c>; the SQL runner
/// stays the single schema authority (devplan D4). When a content type is added, add its DbSet +
/// mapping here to match the migration's columns.
/// </summary>
public class ContentDbContext : DbContext
{
    public ContentDbContext(DbContextOptions<ContentDbContext> options) : base(options) { }

    public DbSet<ContentPing> ContentPings => Set<ContentPing>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<VendorInventory> VendorInventories => Set<VendorInventory>();
    public DbSet<ConversationSet> ConversationSets => Set<ConversationSet>();
    public DbSet<Mob> Mobs => Set<Mob>();
    public DbSet<Faction> Factions => Set<Faction>();
    public DbSet<FactionThreshold> FactionThresholds => Set<FactionThreshold>();
    public DbSet<LootTable> LootTables => Set<LootTable>();
    public DbSet<XpLevel> XpLevels => Set<XpLevel>();
    public DbSet<MobFactionHit> MobFactionHits => Set<MobFactionHit>();
    public DbSet<SpawnTable> SpawnTables => Set<SpawnTable>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Map onto the snake_case columns the migration created. Explicit so EF never guesses
        // (and never tries to own the schema).
        modelBuilder.Entity<ContentPing>(e =>
        {
            e.ToTable("content_ping");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(p => p.Label).HasColumnName("label");
            e.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Item>(e =>
        {
            e.ToTable("items");
            e.HasKey(i => i.ItemId);
            e.Property(i => i.ItemId).HasColumnName("item_id");
            e.Property(i => i.DisplayName).HasColumnName("display_name");
            e.Property(i => i.Description).HasColumnName("description");
            e.Property(i => i.MaxStackSize).HasColumnName("max_stack_size");
            e.Property(i => i.IsEquippable).HasColumnName("is_equippable");
            e.Property(i => i.EquipSlot).HasColumnName("equip_slot");
            e.Property(i => i.BonusStr).HasColumnName("bonus_str");
            e.Property(i => i.BonusSta).HasColumnName("bonus_sta");
            e.Property(i => i.BonusAgi).HasColumnName("bonus_agi");
            e.Property(i => i.BonusDex).HasColumnName("bonus_dex");
            e.Property(i => i.BonusInt).HasColumnName("bonus_int");
            e.Property(i => i.BonusWis).HasColumnName("bonus_wis");
            e.Property(i => i.BonusCha).HasColumnName("bonus_cha");
            e.Property(i => i.WeaponBaseDamage).HasColumnName("weapon_base_damage");
            e.Property(i => i.WeaponDelay).HasColumnName("weapon_delay");
            e.Property(i => i.WeaponRange).HasColumnName("weapon_range");
            e.Property(i => i.WeaponCategory).HasColumnName("weapon_category");
            e.Property(i => i.BuyPrice).HasColumnName("buy_price");
            e.Property(i => i.SellPrice).HasColumnName("sell_price");
            e.Property(i => i.Lore).HasColumnName("lore");
            e.Property(i => i.IconAddress).HasColumnName("icon_address");
            e.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<VendorInventory>(e =>
        {
            e.ToTable("vendor_inventories");
            e.HasKey(v => v.VendorId);
            e.Property(v => v.VendorId).HasColumnName("vendor_id");
            e.Property(v => v.DisplayName).HasColumnName("display_name");
            e.Property(v => v.UpdatedAt).HasColumnName("updated_at");
            e.HasMany(v => v.Items).WithOne().HasForeignKey(i => i.VendorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VendorInventoryItem>(e =>
        {
            e.ToTable("vendor_inventory_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(i => i.VendorId).HasColumnName("vendor_id");
            e.Property(i => i.ItemId).HasColumnName("item_id");
            e.Property(i => i.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<ConversationSet>(e =>
        {
            e.ToTable("conversation_sets");
            e.HasKey(s => s.SetId);
            e.Property(s => s.SetId).HasColumnName("set_id");
            e.Property(s => s.DisplayName).HasColumnName("display_name");
            e.Property(s => s.UpdatedAt).HasColumnName("updated_at");
            e.HasMany(s => s.Keywords).WithOne().HasForeignKey(k => k.SetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationKeywordRow>(e =>
        {
            e.ToTable("conversation_keywords");
            e.HasKey(k => k.Id);
            e.Property(k => k.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(k => k.SetId).HasColumnName("set_id");
            e.Property(k => k.SortOrder).HasColumnName("sort_order");
            e.Property(k => k.Keyword).HasColumnName("keyword");
            e.Property(k => k.Mode).HasColumnName("mode");
            e.Property(k => k.IsOpener).HasColumnName("is_opener");
            e.Property(k => k.EndsConversation).HasColumnName("ends_conversation");
            e.Property(k => k.RequiresUnlock).HasColumnName("requires_unlock");
            e.Property(k => k.Response).HasColumnName("response");
            e.Property(k => k.RequiredFactionId).HasColumnName("required_faction_id");
            e.Property(k => k.RequiredStanding).HasColumnName("required_standing");
            e.Property(k => k.RewardXp).HasColumnName("reward_xp");
            e.Property(k => k.RewardCopper).HasColumnName("reward_copper");
            e.Property(k => k.RequiredCopper).HasColumnName("required_copper");
            e.HasMany(k => k.Unlocks).WithOne().HasForeignKey(u => u.KeywordId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(k => k.RequiredItems).WithOne().HasForeignKey(i => i.KeywordId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(k => k.RewardItems).WithOne().HasForeignKey(i => i.KeywordId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(k => k.FactionHits).WithOne().HasForeignKey(f => f.KeywordId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationKeywordUnlock>(e =>
        {
            e.ToTable("conversation_keyword_unlocks");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(u => u.KeywordId).HasColumnName("keyword_id");
            e.Property(u => u.UnlockedKeyword).HasColumnName("unlocked_keyword");
        });

        modelBuilder.Entity<ConversationKeywordRequiredItem>(e =>
        {
            e.ToTable("conversation_keyword_required_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(i => i.KeywordId).HasColumnName("keyword_id");
            e.Property(i => i.ItemId).HasColumnName("item_id");
            e.Property(i => i.Quantity).HasColumnName("quantity");
        });

        modelBuilder.Entity<ConversationKeywordRewardItem>(e =>
        {
            e.ToTable("conversation_keyword_reward_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(i => i.KeywordId).HasColumnName("keyword_id");
            e.Property(i => i.ItemId).HasColumnName("item_id");
            e.Property(i => i.Quantity).HasColumnName("quantity");
        });

        modelBuilder.Entity<ConversationKeywordFactionHit>(e =>
        {
            e.ToTable("conversation_keyword_faction_hits");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(f => f.KeywordId).HasColumnName("keyword_id");
            e.Property(f => f.FactionId).HasColumnName("faction_id");
            e.Property(f => f.Delta).HasColumnName("delta");
        });

        modelBuilder.Entity<Mob>(e =>
        {
            e.ToTable("mobs");
            e.HasKey(m => m.MobId);
            e.Property(m => m.MobId).HasColumnName("mob_id");
            e.Property(m => m.DisplayName).HasColumnName("display_name");
            e.Property(m => m.MobLevel).HasColumnName("mob_level");
            e.Property(m => m.PrefabAddress).HasColumnName("prefab_address");
            e.Property(m => m.MaxHealth).HasColumnName("max_health");
            e.Property(m => m.AttackDamage).HasColumnName("attack_damage");
            e.Property(m => m.AttackInterval).HasColumnName("attack_interval");
            e.Property(m => m.AttackRange).HasColumnName("attack_range");
            e.Property(m => m.MovementType).HasColumnName("movement_type");
            e.Property(m => m.MoveSpeed).HasColumnName("move_speed");
            e.Property(m => m.WanderRadius).HasColumnName("wander_radius");
            e.Property(m => m.WanderPauseMin).HasColumnName("wander_pause_min");
            e.Property(m => m.WanderPauseMax).HasColumnName("wander_pause_max");
            e.Property(m => m.PerceptionRadius).HasColumnName("perception_radius");
            e.Property(m => m.BaseAggroThreat).HasColumnName("base_aggro_threat");
            e.Property(m => m.FactionId).HasColumnName("faction_id");
            e.Property(m => m.AggroMaxStanding).HasColumnName("aggro_max_standing");
            e.Property(m => m.WarningMaxStanding).HasColumnName("warning_max_standing");
            e.Property(m => m.ConversationSetId).HasColumnName("conversation_set_id");
            e.Property(m => m.LootTableId).HasColumnName("loot_table_id");
            e.Property(m => m.XpReward).HasColumnName("xp_reward");
            e.Property(m => m.VendorId).HasColumnName("vendor_id");
            e.Property(m => m.VendorOpenKeyword).HasColumnName("vendor_open_keyword");
            e.Property(m => m.UpdatedAt).HasColumnName("updated_at");
            e.HasMany(m => m.FactionHits).WithOne().HasForeignKey(h => h.MobId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MobFactionHit>(e =>
        {
            e.ToTable("mob_faction_hits");
            e.HasKey(h => h.Id);
            e.Property(h => h.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(h => h.MobId).HasColumnName("mob_id");
            e.Property(h => h.FactionId).HasColumnName("faction_id");
            e.Property(h => h.Delta).HasColumnName("delta");
            e.Property(h => h.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<Faction>(e =>
        {
            e.ToTable("factions");
            e.HasKey(f => f.FactionId);
            e.Property(f => f.FactionId).HasColumnName("faction_id");
            e.Property(f => f.FactionName).HasColumnName("faction_name");
            e.Property(f => f.UpdatedAt).HasColumnName("updated_at");
            e.HasMany(f => f.Relations).WithOne().HasForeignKey(r => r.FactionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(f => f.RaceDefaults).WithOne().HasForeignKey(d => d.FactionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FactionRelation>(e =>
        {
            e.ToTable("faction_relations");
            e.HasKey(r => new { r.FactionId, r.OtherFactionId, r.Relation });
            e.Property(r => r.FactionId).HasColumnName("faction_id");
            e.Property(r => r.OtherFactionId).HasColumnName("other_faction_id");
            e.Property(r => r.Relation).HasColumnName("relation");
        });

        modelBuilder.Entity<RaceFactionDefault>(e =>
        {
            e.ToTable("race_faction_defaults");
            e.HasKey(d => new { d.Race, d.FactionId });
            e.Property(d => d.Race).HasColumnName("race");
            e.Property(d => d.FactionId).HasColumnName("faction_id");
            e.Property(d => d.Score).HasColumnName("score");
        });

        modelBuilder.Entity<FactionThreshold>(e =>
        {
            e.ToTable("faction_thresholds");
            e.HasKey(t => t.Name);
            e.Property(t => t.Name).HasColumnName("name");
            e.Property(t => t.MinScore).HasColumnName("min_score");
            e.Property(t => t.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<LootTable>(e =>
        {
            e.ToTable("loot_tables");
            e.HasKey(t => t.LootTableId);
            e.Property(t => t.LootTableId).HasColumnName("loot_table_id");
            e.Property(t => t.DisplayName).HasColumnName("display_name");
            e.Property(t => t.UpdatedAt).HasColumnName("updated_at");
            e.HasMany(t => t.Items).WithOne().HasForeignKey(i => i.LootTableId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(t => t.DropCounts).WithOne().HasForeignKey(d => d.LootTableId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(t => t.CoinTiers).WithOne().HasForeignKey(c => c.LootTableId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LootItem>(e =>
        {
            e.ToTable("loot_table_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(i => i.LootTableId).HasColumnName("loot_table_id");
            e.Property(i => i.ItemId).HasColumnName("item_id");
            e.Property(i => i.Weight).HasColumnName("weight");
            e.Property(i => i.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<LootDropCount>(e =>
        {
            e.ToTable("loot_table_drop_counts");
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(d => d.LootTableId).HasColumnName("loot_table_id");
            e.Property(d => d.Count).HasColumnName("count");
            e.Property(d => d.Weight).HasColumnName("weight");
            e.Property(d => d.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<LootCoinTier>(e =>
        {
            e.ToTable("loot_table_coin_tiers");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(c => c.LootTableId).HasColumnName("loot_table_id");
            e.Property(c => c.MinCopper).HasColumnName("min_copper");
            e.Property(c => c.MaxCopper).HasColumnName("max_copper");
            e.Property(c => c.Weight).HasColumnName("weight");
            e.Property(c => c.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<XpLevel>(e =>
        {
            e.ToTable("xp_levels");
            e.HasKey(x => x.Level);
            e.Property(x => x.Level).HasColumnName("level").ValueGeneratedNever();
            e.Property(x => x.XpToNext).HasColumnName("xp_to_next");
        });

        modelBuilder.Entity<SpawnTable>(e =>
        {
            e.ToTable("spawn_tables");
            e.HasKey(t => t.SpawnTableId);
            e.Property(t => t.SpawnTableId).HasColumnName("spawn_table_id");
            e.Property(t => t.DisplayName).HasColumnName("display_name");
            e.Property(t => t.TimerBaseSeconds).HasColumnName("timer_base_seconds");
            e.Property(t => t.TimerVariance).HasColumnName("timer_variance");
            e.Property(t => t.UpdatedAt).HasColumnName("updated_at");
            e.HasMany(t => t.Entries).WithOne().HasForeignKey(x => x.SpawnTableId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SpawnTableEntry>(e =>
        {
            e.ToTable("spawn_table_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.SpawnTableId).HasColumnName("spawn_table_id");
            e.Property(x => x.MobId).HasColumnName("mob_id");
            e.Property(x => x.Weight).HasColumnName("weight");
            e.Property(x => x.GroupSize).HasColumnName("group_size");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
        });
    }
}
