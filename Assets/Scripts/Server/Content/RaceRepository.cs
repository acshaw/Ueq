using System.Collections.Generic;
using Npgsql;

/// <summary>Read-only repository over <c>races</c> (M2.10), following the 1.2 DAL convention. Flat, no children.</summary>
public sealed class RaceRepository : IRepository
{
    public List<RaceSnapshot> LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var rows = new List<RaceSnapshot>();
        using var cmd = new NpgsqlCommand(
            "SELECT race_id, race_name, xp_modifier, str_mod, sta_mod, agi_mod, dex_mod, int_mod, wis_mod, cha_mod " +
            "FROM races ORDER BY race_id", conn, tx);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new RaceSnapshot
            {
                RaceId     = reader.GetString(0),
                RaceName   = reader.GetString(1),
                XpModifier = reader.GetFloat(2),
                StrMod     = reader.GetInt32(3),
                StaMod     = reader.GetInt32(4),
                AgiMod     = reader.GetInt32(5),
                DexMod     = reader.GetInt32(6),
                IntMod     = reader.GetInt32(7),
                WisMod     = reader.GetInt32(8),
                ChaMod     = reader.GetInt32(9),
            });
        }
        return rows;
    }
}
