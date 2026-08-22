using System.Collections.Generic;
using Npgsql;

/// <summary>
/// Read-only repository over <c>classes</c> (M2.10), following the 1.2 DAL convention. Header rows
/// first (into an id-keyed map), then the ordered <c>class_starting_abilities</c> children — so a class
/// with no starting abilities still loads.
/// </summary>
public sealed class ClassRepository : IRepository
{
    public List<ClassSnapshot> LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var byId  = new Dictionary<string, ClassSnapshot>();
        var order = new List<string>();

        using (var cmd = new NpgsqlCommand(
            "SELECT class_id, class_name, xp_modifier, " +
            "base_str, base_sta, base_agi, base_dex, base_int, base_wis, base_cha, " +
            "class_base_hp, hp_per_level, sta_cap, base_sta_ratio, sta_growth_rate, " +
            "mana_stat_type, class_base_mana, mana_per_level, mana_cap, base_mana_ratio, mana_growth_rate " +
            "FROM classes ORDER BY class_id", conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var id = reader.GetString(0);
                byId[id] = new ClassSnapshot
                {
                    ClassId          = id,
                    ClassName        = reader.GetString(1),
                    XpModifier       = reader.GetFloat(2),
                    BaseStr          = reader.GetInt32(3),
                    BaseSta          = reader.GetInt32(4),
                    BaseAgi          = reader.GetInt32(5),
                    BaseDex          = reader.GetInt32(6),
                    BaseInt          = reader.GetInt32(7),
                    BaseWis          = reader.GetInt32(8),
                    BaseCha          = reader.GetInt32(9),
                    ClassBaseHP      = reader.GetInt32(10),
                    HpPerLevel       = reader.GetInt32(11),
                    StaCap           = reader.GetInt32(12),
                    BaseStaRatio     = reader.GetFloat(13),
                    StaGrowthRate    = reader.GetFloat(14),
                    ManaStatType     = reader.GetInt32(15),
                    ClassBaseMana    = reader.GetInt32(16),
                    ManaPerLevel     = reader.GetInt32(17),
                    ManaCap          = reader.GetInt32(18),
                    BaseManaRatio    = reader.GetFloat(19),
                    ManaGrowthRate   = reader.GetFloat(20),
                    StartingAbilityIds = new List<string>(),
                };
                order.Add(id);
            }
        }

        using (var cmd = new NpgsqlCommand(
            "SELECT class_id, ability_id FROM class_starting_abilities ORDER BY class_id, sort_order, id",
            conn, tx))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                if (byId.TryGetValue(reader.GetString(0), out var s))
                    s.StartingAbilityIds.Add(reader.GetString(1));
        }

        var rows = new List<ClassSnapshot>(order.Count);
        foreach (var id in order) rows.Add(byId[id]);
        return rows;
    }
}
