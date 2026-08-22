using System.Collections.Generic;
using Npgsql;

/// <summary>
/// Hand-written Npgsql repository over <c>characters</c> + its child tables (1.3). Follows the 1.2
/// convention: parameterized commands only; reads take an optional transaction so they work inside a
/// worker job's tx or on a standalone load connection.
///
/// The character is always loaded/saved as one atomic unit keyed by account. <see cref="Upsert"/>
/// upserts the scalar row (insert-or-update on the account UNIQUE), then rewrites each child table
/// with delete-all-for-character + insert — small fixed row counts, no per-slot diffing.
/// </summary>
public sealed class CharacterRepository : IRepository
{
    // ── Write (worker thread, inside the job's transaction) ─────────────────────

    public void Upsert(NpgsqlConnection conn, NpgsqlTransaction tx, CharacterSnapshot s)
    {
        long characterId = UpsertCore(conn, tx, s);

        DeleteChildren(conn, tx, "character_inventory",      characterId);
        DeleteChildren(conn, tx, "character_equipment",      characterId);
        DeleteChildren(conn, tx, "character_faction_scores", characterId);
        DeleteChildren(conn, tx, "character_hotbar",         characterId);

        InsertInventory(conn, tx, characterId, s.Inventory);
        InsertEquipment(conn, tx, characterId, s.Equipment);
        InsertFactionScores(conn, tx, characterId, s.FactionScores);
        InsertHotbar(conn, tx, characterId, s.Hotbar);
    }

    // UPDATE by character_id (1.6). The row always exists by save time — created up front by
    // CreateIdentity at character creation (decision O2) or loaded from an existing character — so a
    // plain UPDATE keyed on the PK is correct, and saves never cross-contaminate characters on the
    // same account (the bug a former account-id-keyed upsert would cause with multiple characters).
    long UpsertCore(NpgsqlConnection conn, NpgsqlTransaction tx, CharacterSnapshot s)
    {
        using var cmd = new NpgsqlCommand(
            "UPDATE characters SET " +
            " name = @name, gender = @gender, race_name = @race, class_name = @class, total_xp = @xp, " +
            " copper = @cp, silver = @sp, gold = @gp, platinum = @pp, " +
            " current_health = @hp, current_mana = @mp, " +
            " might_skill = @might, finesse_skill = @finesse, offense_skill = @offense, " +
            " defense_skill = @defense, dodge_skill = @dodge, parry_skill = @parry, riposte_skill = @riposte, " +
            " pos_x = @px, pos_y = @py, pos_z = @pz, yaw = @yaw, " +
            " bind_x = @bx, bind_y = @by, bind_z = @bz, " +
            " zone_id = @zone, " +
            " actual_race = @actual, apparent_race = @apparent, updated_at = now() " +
            "WHERE character_id = @cid", conn, tx);

        cmd.Parameters.AddWithValue("cid", s.CharacterId);
        cmd.Parameters.AddWithValue("name", s.Name ?? "");
        cmd.Parameters.AddWithValue("gender", s.Gender.ToString());
        cmd.Parameters.AddWithValue("race", s.RaceName ?? "");
        cmd.Parameters.AddWithValue("class", s.ClassName ?? "");
        cmd.Parameters.AddWithValue("xp", s.TotalXp);
        cmd.Parameters.AddWithValue("cp", s.Copper);
        cmd.Parameters.AddWithValue("sp", s.Silver);
        cmd.Parameters.AddWithValue("gp", s.Gold);
        cmd.Parameters.AddWithValue("pp", s.Platinum);
        cmd.Parameters.AddWithValue("hp", s.CurrentHealth);
        cmd.Parameters.AddWithValue("mp", s.CurrentMana);
        cmd.Parameters.AddWithValue("might", s.MightSkill);
        cmd.Parameters.AddWithValue("finesse", s.FinesseSkill);
        cmd.Parameters.AddWithValue("offense", s.Offense);
        cmd.Parameters.AddWithValue("defense", s.DefenseSkill);
        cmd.Parameters.AddWithValue("dodge", s.DodgeSkill);
        cmd.Parameters.AddWithValue("parry", s.ParrySkill);
        cmd.Parameters.AddWithValue("riposte", s.RiposteSkill);
        cmd.Parameters.AddWithValue("px", s.PosX);
        cmd.Parameters.AddWithValue("py", s.PosY);
        cmd.Parameters.AddWithValue("pz", s.PosZ);
        cmd.Parameters.AddWithValue("yaw", s.Yaw);
        cmd.Parameters.AddWithValue("bx", s.BindX);
        cmd.Parameters.AddWithValue("by", s.BindY);
        cmd.Parameters.AddWithValue("bz", s.BindZ);
        cmd.Parameters.AddWithValue("zone", s.ZoneId ?? ZoneCatalog.DefaultStarterZoneId);
        cmd.Parameters.AddWithValue("actual", s.ActualRace ?? "");
        cmd.Parameters.AddWithValue("apparent", s.ApparentRace ?? "");
        cmd.ExecuteNonQuery();
        return s.CharacterId;
    }

    /// <summary>Create the identity row for a new character at creation time (decision O2), returning
    /// its generated <c>character_id</c>. Scalars default (level-1); the full state is written by the
    /// first save once the player spawns.</summary>
    public long CreateIdentity(NpgsqlConnection conn, long accountId, string name, string gender, string raceName, string className, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO characters (account_id, name, gender, race_name, class_name) " +
            "VALUES (@aid, @name, @gender, @race, @class) RETURNING character_id", conn, tx);
        cmd.Parameters.AddWithValue("aid", accountId);
        cmd.Parameters.AddWithValue("name", name ?? "");
        cmd.Parameters.AddWithValue("gender", gender ?? Gender.Male.ToString());
        cmd.Parameters.AddWithValue("race", raceName ?? "");
        cmd.Parameters.AddWithValue("class", className ?? "");
        return (long)cmd.ExecuteScalar();
    }

    /// <summary>Number of characters on an account (for the slot cap, O1).</summary>
    public int CountByAccount(NpgsqlConnection conn, long accountId, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand("SELECT count(*) FROM characters WHERE account_id = @aid", conn, tx);
        cmd.Parameters.AddWithValue("aid", accountId);
        return (int)(long)cmd.ExecuteScalar();
    }

    static void DeleteChildren(NpgsqlConnection conn, NpgsqlTransaction tx, string table, long characterId)
    {
        using var cmd = new NpgsqlCommand($"DELETE FROM {table} WHERE character_id = @cid", conn, tx);
        cmd.Parameters.AddWithValue("cid", characterId);
        cmd.ExecuteNonQuery();
    }

    static void InsertInventory(NpgsqlConnection conn, NpgsqlTransaction tx, long characterId, InvEntry[] inv)
    {
        if (inv == null) return;
        for (int i = 0; i < inv.Length; i++)
        {
            if (string.IsNullOrEmpty(inv[i].Id) || inv[i].Q <= 0) continue; // store non-empty slots only
            using var cmd = new NpgsqlCommand(
                "INSERT INTO character_inventory (character_id, slot_index, item_id, quantity) " +
                "VALUES (@cid, @slot, @item, @qty)", conn, tx);
            cmd.Parameters.AddWithValue("cid", characterId);
            cmd.Parameters.AddWithValue("slot", i);
            cmd.Parameters.AddWithValue("item", inv[i].Id);
            cmd.Parameters.AddWithValue("qty", inv[i].Q);
            cmd.ExecuteNonQuery();
        }
    }

    static void InsertEquipment(NpgsqlConnection conn, NpgsqlTransaction tx, long characterId, string[] equip)
    {
        if (equip == null) return;
        for (int i = 0; i < equip.Length; i++)
        {
            if (string.IsNullOrEmpty(equip[i])) continue;
            using var cmd = new NpgsqlCommand(
                "INSERT INTO character_equipment (character_id, slot, item_id) VALUES (@cid, @slot, @item)",
                conn, tx);
            cmd.Parameters.AddWithValue("cid", characterId);
            cmd.Parameters.AddWithValue("slot", i);
            cmd.Parameters.AddWithValue("item", equip[i]);
            cmd.ExecuteNonQuery();
        }
    }

    static void InsertFactionScores(NpgsqlConnection conn, NpgsqlTransaction tx, long characterId, Dictionary<string, int> scores)
    {
        if (scores == null) return;
        foreach (var kv in scores)
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;
            using var cmd = new NpgsqlCommand(
                "INSERT INTO character_faction_scores (character_id, faction_id, score) " +
                "VALUES (@cid, @faction, @score)", conn, tx);
            cmd.Parameters.AddWithValue("cid", characterId);
            cmd.Parameters.AddWithValue("faction", kv.Key);
            cmd.Parameters.AddWithValue("score", kv.Value);
            cmd.ExecuteNonQuery();
        }
    }

    static void InsertHotbar(NpgsqlConnection conn, NpgsqlTransaction tx, long characterId, string[] hotbar)
    {
        if (hotbar == null) return;
        for (int i = 0; i < hotbar.Length; i++)
        {
            if (string.IsNullOrEmpty(hotbar[i])) continue;
            using var cmd = new NpgsqlCommand(
                "INSERT INTO character_hotbar (character_id, slot_index, ability_id) VALUES (@cid, @slot, @ability)",
                conn, tx);
            cmd.Parameters.AddWithValue("cid", characterId);
            cmd.Parameters.AddWithValue("slot", i);
            cmd.Parameters.AddWithValue("ability", hotbar[i]);
            cmd.ExecuteNonQuery();
        }
    }

    // ── Read (off-thread; result re-applied on the main thread) ─────────────────

    /// <returns>The character with this id, or <c>null</c> if it doesn't exist. Keyed on
    /// <c>character_id</c> (1.6) — an account can now have several characters.</returns>
    public CharacterSnapshot Load(NpgsqlConnection conn, long characterId, NpgsqlTransaction tx = null)
    {
        CharacterSnapshot s;

        // Read the scalar row first; the reader must be closed before issuing the child queries
        // (Npgsql forbids a second command on the same connection while a reader is open).
        using (var cmd = new NpgsqlCommand(
            "SELECT character_id, account_id, name, race_name, class_name, total_xp, copper, silver, gold, platinum, " +
            "current_health, current_mana, pos_x, pos_y, pos_z, yaw, bind_x, bind_y, bind_z, " +
            "actual_race, apparent_race, zone_id, gender, might_skill, finesse_skill, offense_skill, " +
            "defense_skill, dodge_skill, parry_skill, riposte_skill FROM characters WHERE character_id = @cid", conn, tx))
        {
            cmd.Parameters.AddWithValue("cid", characterId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            s = new CharacterSnapshot
            {
                CharacterId   = r.GetInt64(0),
                AccountId     = r.GetInt64(1),
                Name          = r.GetString(2),
                RaceName      = r.GetString(3),
                ClassName     = r.GetString(4),
                TotalXp       = r.GetInt32(5),
                Copper        = r.GetInt32(6),
                Silver        = r.GetInt32(7),
                Gold          = r.GetInt32(8),
                Platinum      = r.GetInt32(9),
                CurrentHealth = r.GetInt32(10),
                CurrentMana   = r.GetInt32(11),
                PosX          = r.GetFloat(12),
                PosY          = r.GetFloat(13),
                PosZ          = r.GetFloat(14),
                Yaw           = r.GetFloat(15),
                BindX         = r.GetFloat(16),
                BindY         = r.GetFloat(17),
                BindZ         = r.GetFloat(18),
                ActualRace    = r.GetString(19),
                ApparentRace  = r.GetString(20),
                ZoneId        = r.GetString(21),
                Gender        = ParseGender(r.GetString(22)),
                MightSkill    = r.GetInt32(23),
                FinesseSkill  = r.GetInt32(24),
                Offense       = r.GetInt32(25),
                DefenseSkill  = r.GetInt32(26),
                DodgeSkill    = r.GetInt32(27),
                ParrySkill    = r.GetInt32(28),
                RiposteSkill  = r.GetInt32(29),
            };
        }

        s.Inventory     = LoadInventory(conn, tx, s.CharacterId);
        s.Equipment     = LoadEquipment(conn, tx, s.CharacterId);
        s.FactionScores = LoadFactionScores(conn, tx, s.CharacterId);
        s.Hotbar        = LoadHotbar(conn, tx, s.CharacterId);
        return s;
    }

    static InvEntry[] LoadInventory(NpgsqlConnection conn, NpgsqlTransaction tx, long characterId)
    {
        var arr = new InvEntry[PlayerInventory.SlotCount];
        for (int i = 0; i < arr.Length; i++) arr[i] = new InvEntry { Id = "", Q = 0 };

        using var cmd = new NpgsqlCommand(
            "SELECT slot_index, item_id, quantity FROM character_inventory WHERE character_id = @cid", conn, tx);
        cmd.Parameters.AddWithValue("cid", characterId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            int slot = r.GetInt32(0);
            if ((uint)slot < (uint)arr.Length)
                arr[slot] = new InvEntry { Id = r.GetString(1), Q = r.GetInt32(2) };
        }
        return arr;
    }

    static string[] LoadEquipment(NpgsqlConnection conn, NpgsqlTransaction tx, long characterId)
    {
        var arr = new string[EquipSlotUtil.Count];
        for (int i = 0; i < arr.Length; i++) arr[i] = "";

        using var cmd = new NpgsqlCommand(
            "SELECT slot, item_id FROM character_equipment WHERE character_id = @cid", conn, tx);
        cmd.Parameters.AddWithValue("cid", characterId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            int slot = r.GetInt32(0);
            if ((uint)slot < (uint)arr.Length)
                arr[slot] = r.GetString(1);
        }
        return arr;
    }

    static Dictionary<string, int> LoadFactionScores(NpgsqlConnection conn, NpgsqlTransaction tx, long characterId)
    {
        var d = new Dictionary<string, int>();
        using var cmd = new NpgsqlCommand(
            "SELECT faction_id, score FROM character_faction_scores WHERE character_id = @cid", conn, tx);
        cmd.Parameters.AddWithValue("cid", characterId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            d[r.GetString(0)] = r.GetInt32(1);
        return d;
    }

    static string[] LoadHotbar(NpgsqlConnection conn, NpgsqlTransaction tx, long characterId)
    {
        var arr = new string[PlayerAbilities.HotbarSize];
        for (int i = 0; i < arr.Length; i++) arr[i] = "";

        using var cmd = new NpgsqlCommand(
            "SELECT slot_index, ability_id FROM character_hotbar WHERE character_id = @cid", conn, tx);
        cmd.Parameters.AddWithValue("cid", characterId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            int slot = r.GetInt32(0);
            if ((uint)slot < (uint)arr.Length)
                arr[slot] = r.GetString(1);
        }
        return arr;
    }

    // ── Character-select reads (1.5) ────────────────────────────────────────────

    /// <summary>Raw row data for the character-select list. Level is derived on the main thread
    /// (it touches the Resources-loaded XP table) — keep it out of the off-thread read.</summary>
    public struct CharacterRow
    {
        public long   Id;
        public string Name;
        public Gender Gender;
        public string Race;
        public string Class;
        public int    TotalXp;
    }

    /// <summary>All characters for an account (one in 1.5; multiple in 1.6).</summary>
    public List<CharacterRow> ListByAccount(NpgsqlConnection conn, long accountId, NpgsqlTransaction tx = null)
    {
        var list = new List<CharacterRow>();
        using var cmd = new NpgsqlCommand(
            "SELECT character_id, name, race_name, class_name, total_xp, gender FROM characters " +
            "WHERE account_id = @aid ORDER BY character_id", conn, tx);
        cmd.Parameters.AddWithValue("aid", accountId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new CharacterRow
            {
                Id = r.GetInt64(0), Name = r.GetString(1), Race = r.GetString(2),
                Class = r.GetString(3), TotalXp = r.GetInt32(4), Gender = ParseGender(r.GetString(5)),
            });
        return list;
    }

    static Gender ParseGender(string s)
        => System.Enum.TryParse(s, out Gender g) ? g : Gender.Male;

    /// <summary>Case-insensitive name-taken check (the DB unique index is the real guard).</summary>
    public bool NameExists(NpgsqlConnection conn, string nameLower, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand("SELECT 1 FROM characters WHERE lower(name) = @n LIMIT 1", conn, tx);
        cmd.Parameters.AddWithValue("n", nameLower);
        return cmd.ExecuteScalar() != null;
    }

    // ── Delete (editor tooling + 1.5 character select) ──────────────────────────

    /// <summary>Delete all characters on an account (children cascade) — editor wipe tool.</summary>
    public int DeleteByAccount(NpgsqlConnection conn, long accountId, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand("DELETE FROM characters WHERE account_id = @aid", conn, tx);
        cmd.Parameters.AddWithValue("aid", accountId);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>Delete one character, guarded by account ownership (children cascade). 1.6 select screen.</summary>
    public int DeleteById(NpgsqlConnection conn, long accountId, long characterId, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand(
            "DELETE FROM characters WHERE character_id = @cid AND account_id = @aid", conn, tx);
        cmd.Parameters.AddWithValue("cid", characterId);
        cmd.Parameters.AddWithValue("aid", accountId);
        return cmd.ExecuteNonQuery();
    }
}
