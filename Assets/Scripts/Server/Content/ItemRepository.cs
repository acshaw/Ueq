using System.Collections.Generic;
using Npgsql;

/// <summary>
/// Read-only repository over the <c>items</c> table (M2.2), following the 1.2 DAL convention
/// (hand-written Npgsql, parameterized, reads take an optional transaction). The web API owns
/// writes; the game server only loads. This is the reference shape the later content repositories
/// (abilities, mobs, …) copy.
/// </summary>
public sealed class ItemRepository : IRepository
{
    public List<ItemSnapshot> LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var rows = new List<ItemSnapshot>();
        using var cmd = new NpgsqlCommand(
            "SELECT item_id, display_name, description, max_stack_size, " +
            "is_equippable, equip_slot, " +
            "bonus_str, bonus_sta, bonus_agi, bonus_dex, bonus_int, bonus_wis, bonus_cha, bonus_ac, " +
            "weapon_base_damage, weapon_bonus_damage, weapon_delay, weapon_range, weapon_category, " +
            "buy_price, sell_price, icon_address, lore " +
            "FROM items ORDER BY item_id", conn, tx);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ItemSnapshot
            {
                ItemId            = reader.GetString(0),
                DisplayName       = reader.GetString(1),
                Description       = reader.GetString(2),
                MaxStackSize      = reader.GetInt32(3),
                IsEquippable      = reader.GetBoolean(4),
                EquipSlot         = reader.GetInt32(5),
                BonusStr          = reader.GetInt32(6),
                BonusSta          = reader.GetInt32(7),
                BonusAgi          = reader.GetInt32(8),
                BonusDex          = reader.GetInt32(9),
                BonusInt          = reader.GetInt32(10),
                BonusWis          = reader.GetInt32(11),
                BonusCha          = reader.GetInt32(12),
                BonusAc           = reader.GetInt32(13),
                WeaponBaseDamage  = reader.GetInt32(14),
                WeaponBonusDamage = reader.GetInt32(15),
                WeaponDelay       = reader.GetFloat(16),
                WeaponRange       = reader.GetFloat(17),
                WeaponCategory    = reader.GetInt32(18),
                BuyPrice          = reader.GetInt32(19),
                SellPrice         = reader.GetInt32(20),
                IconAddress       = reader.IsDBNull(21) ? null : reader.GetString(21),
                Lore              = reader.GetBoolean(22),
            });
        }
        return rows;
    }
}
