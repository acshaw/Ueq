using System.Collections.Generic;
using Npgsql;

/// <summary>Plain-data view of one mob (M2.5). Mirrors MobDefinition; refs are ids resolved at build.</summary>
public struct MobSnapshot
{
    public string MobId;
    public string DisplayName;
    public int    MobLevel;
    public string PrefabAddress;

    public int    MaxHealth;
    public int    AttackDamage;
    public float  AttackInterval;
    public float  AttackRange;

    public int    MovementType;
    public float  MoveSpeed;
    public float  WanderRadius;
    public float  WanderPauseMin;
    public float  WanderPauseMax;

    public float  PerceptionRadius;
    public int    BaseAggroThreat;

    public string FactionId;
    public string AggroMaxStanding;
    public string WarningMaxStanding;

    public string ConversationSetId;
    public string LootTableId;
    public int    XpReward;

    public string VendorId;
    public string VendorOpenKeyword;

    public List<MobFactionHitSnapshot> FactionHits;   // M2.7.1

    // 5.1.1 (HR5) / 5.1.2 (AV3) / 2.12 (SK5) — combat pipeline data, authored per mob. WeaponCategory/
    // WeaponSkill are no longer consumed by the resolver as of 5.1.5 (superseded by Atk).
    public int   WeaponCategory;
    public int   WeaponSkill;
    public float Atk; // 5.1.5 (AD3)
    public bool  AttackIsParryable;
    public float AvoidanceDodge, AvoidanceParry, AvoidanceRiposte; // 2026-08-13 follow-up (AV3)
    public float Ac; // 2026-08-21 (Mitigation) — authored directly, same reasoning as Atk/Avoidance*

    // 5.4 (AG3) — social aggro, opt-in per mob.
    public bool  SocialAggroEnabled;
    public float SocialAggroRadius;
}

/// <summary>One faction adjustment applied to the killer on this mob's death (M2.7.1).</summary>
public struct MobFactionHitSnapshot
{
    public string FactionId;
    public int    Delta;
}

/// <summary>
/// Read-only repository over <c>mobs</c> (M2.5). Server-only (the server spawns mobs; clients get them
/// as normal spawned NetworkIdentities, not a catalog). 1.2 DAL convention.
/// </summary>
public sealed class MobRepository : IRepository
{
    public List<MobSnapshot> LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        // Header rows first (into an id-keyed map), then attach the faction-hit children — so a mob with
        // no hits still loads. FactionHits is a reference type, so the copy added to `order`/result shares
        // the list mutated in the child pass below.
        var byId  = new Dictionary<string, MobSnapshot>();
        var order = new List<string>();

        using (var cmd = new NpgsqlCommand(
            "SELECT mob_id, display_name, mob_level, prefab_address, " +
            "max_health, attack_damage, attack_interval, attack_range, " +
            "movement_type, move_speed, wander_radius, wander_pause_min, wander_pause_max, " +
            "perception_radius, base_aggro_threat, " +
            "faction_id, aggro_max_standing, warning_max_standing, " +
            "conversation_set_id, loot_table_id, xp_reward, " +
            "vendor_id, vendor_open_keyword, " +
            "weapon_category, weapon_skill, atk, " +
            "attack_is_parryable, avoidance_dodge, avoidance_parry, avoidance_riposte, ac, " +
            "social_aggro_enabled, social_aggro_radius " +
            "FROM mobs ORDER BY mob_id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetString(0);
                byId[id] = new MobSnapshot
                {
                    MobId              = id,
                    DisplayName        = reader.GetString(1),
                    MobLevel           = reader.GetInt32(2),
                    PrefabAddress      = reader.IsDBNull(3) ? null : reader.GetString(3),
                    MaxHealth          = reader.GetInt32(4),
                    AttackDamage       = reader.GetInt32(5),
                    AttackInterval     = reader.GetFloat(6),
                    AttackRange        = reader.GetFloat(7),
                    MovementType       = reader.GetInt32(8),
                    MoveSpeed          = reader.GetFloat(9),
                    WanderRadius       = reader.GetFloat(10),
                    WanderPauseMin     = reader.GetFloat(11),
                    WanderPauseMax     = reader.GetFloat(12),
                    PerceptionRadius   = reader.GetFloat(13),
                    BaseAggroThreat    = reader.GetInt32(14),
                    FactionId          = reader.IsDBNull(15) ? null : reader.GetString(15),
                    AggroMaxStanding   = reader.GetString(16),
                    WarningMaxStanding = reader.GetString(17),
                    ConversationSetId  = reader.IsDBNull(18) ? null : reader.GetString(18),
                    LootTableId        = reader.IsDBNull(19) ? null : reader.GetString(19),
                    XpReward           = reader.GetInt32(20),
                    VendorId           = reader.IsDBNull(21) ? null : reader.GetString(21),
                    VendorOpenKeyword  = reader.GetString(22),
                    WeaponCategory     = reader.GetInt32(23),
                    WeaponSkill        = reader.GetInt32(24),
                    Atk                = reader.GetFloat(25),
                    AttackIsParryable  = reader.GetBoolean(26),
                    AvoidanceDodge     = reader.GetFloat(27),
                    AvoidanceParry     = reader.GetFloat(28),
                    AvoidanceRiposte   = reader.GetFloat(29),
                    Ac                 = reader.GetFloat(30),
                    SocialAggroEnabled = reader.GetBoolean(31),
                    SocialAggroRadius  = reader.GetFloat(32),
                    FactionHits        = new List<MobFactionHitSnapshot>(),
                };
                order.Add(id);
            }
        }

        using (var cmd = new NpgsqlCommand(
            "SELECT mob_id, faction_id, delta FROM mob_faction_hits ORDER BY mob_id, sort_order, id",
            conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                if (byId.TryGetValue(reader.GetString(0), out var s))
                    s.FactionHits.Add(new MobFactionHitSnapshot
                    {
                        FactionId = reader.GetString(1),
                        Delta     = reader.GetInt32(2),
                    });
        }

        var rows = new List<MobSnapshot>(order.Count);
        foreach (var id in order) rows.Add(byId[id]);
        return rows;
    }
}
