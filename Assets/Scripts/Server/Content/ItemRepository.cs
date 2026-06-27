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
            "bonus_str, bonus_sta, bonus_agi, bonus_dex, bonus_int, bonus_wis, bonus_cha, " +
            "weapon_base_damage, weapon_delay, weapon_range, weapon_category, " +
            "buy_price, sell_price, icon_address " +
            "FROM items ORDER BY item_id", conn, tx);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ItemSnapshot
            {
                ItemId           = reader.GetString(0),
                DisplayName      = reader.GetString(1),
                Description      = reader.GetString(2),
                MaxStackSize     = reader.GetInt32(3),
                IsEquippable     = reader.GetBoolean(4),
                EquipSlot        = reader.GetInt32(5),
                BonusStr         = reader.GetInt32(6),
                BonusSta         = reader.GetInt32(7),
                BonusAgi         = reader.GetInt32(8),
                BonusDex         = reader.GetInt32(9),
                BonusInt         = reader.GetInt32(10),
                BonusWis         = reader.GetInt32(11),
                BonusCha         = reader.GetInt32(12),
                WeaponBaseDamage = reader.GetInt32(13),
                WeaponDelay      = reader.GetFloat(14),
                WeaponRange      = reader.GetFloat(15),
                WeaponCategory   = reader.GetInt32(16),
                BuyPrice         = reader.GetInt32(17),
                SellPrice        = reader.GetInt32(18),
                IconAddress      = reader.IsDBNull(19) ? null : reader.GetString(19),
            });
        }
        return rows;
    }
}
