using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;

/// <summary>
/// Generic, schema-driven content export/import (M2.11, SE2/SE3). Deliberately has zero dependency on
/// any content type's repository/registry/entity code — it enumerates tables via
/// <c>information_schema</c> at runtime, so it needs no changes when a future content type (2.12+) adds
/// a new table. "Content" = every base table in the public schema minus a small player/account-state
/// exclude-list (mirrors the content-vs-player-state split documented in roadmap.md).
///
/// Every value round-trips as its canonical STRING form (not a raw JSON number/bool/date) so import never
/// has to guess a type back out of ambiguous JSON — the exported column's Postgres type (captured at
/// export time) tells import exactly how to parse each string and which NpgsqlDbType to bind it as.
///
/// Import is a full wipe-and-reload inside one transaction: TRUNCATE every content table, disable FK/
/// trigger enforcement for the session (a standard Postgres bulk-reload pattern — requires the connected
/// role to have that privilege, true of the default local dev superuser), insert every row in any order,
/// then reset each table's serial sequence from its own MAX(id) so future API-driven inserts don't
/// collide with imported ids.
/// </summary>
public static class ContentExportImport
{
    // Player/account runtime state — never part of a content export/import. Mirrors roadmap.md's
    // stated content-vs-player-state split. content_ping is the dead 2.1 smoke table (harmless to skip).
    static readonly HashSet<string> ExcludedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "schema_version", "accounts", "characters",
        "character_inventory", "character_equipment", "character_faction_scores", "character_hotbar",
        "content_ping",
    };

    public class ColumnSpec
    {
        public string Name;
        public string PgType; // information_schema.columns.data_type, e.g. "text", "integer"
    }

    public class TableExport
    {
        public List<ColumnSpec> Columns = new();
        public List<string[]>   Rows    = new(); // each entry aligns with Columns; null = SQL NULL
    }

    public class ContentExport
    {
        public string ExportedAt;
        public Dictionary<string, TableExport> Tables = new();
    }

    // ── Export ───────────────────────────────────────────────────────────────────

    public static ContentExport Export(NpgsqlConnection conn)
    {
        var export = new ContentExport { ExportedAt = DateTime.UtcNow.ToString("O") };

        foreach (var table in ListContentTables(conn))
        {
            var columns = ListColumns(conn, table);
            var t = new TableExport { Columns = columns };

            var selectList = string.Join(", ", columns.ConvertAll(c => $"\"{c.Name}\""));
            using var cmd = new NpgsqlCommand($"SELECT {selectList} FROM \"{table}\" ORDER BY 1", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new string[columns.Count];
                for (int i = 0; i < columns.Count; i++)
                    row[i] = reader.IsDBNull(i) ? null : ToCanonicalString(reader.GetValue(i), columns[i].PgType);
                t.Rows.Add(row);
            }

            export.Tables[table] = t;
        }

        return export;
    }

    public static int ExportToFile(NpgsqlConnection conn, string path)
    {
        var export = Export(conn);
        File.WriteAllText(path, JsonConvert.SerializeObject(export, Formatting.Indented));
        int totalRows = 0;
        foreach (var t in export.Tables.Values) totalRows += t.Rows.Count;
        return totalRows;
    }

    // ── Import ───────────────────────────────────────────────────────────────────

    public static void ImportFromFile(NpgsqlConnection conn, string path)
    {
        var export = JsonConvert.DeserializeObject<ContentExport>(File.ReadAllText(path));
        Import(conn, export);
    }

    public static void Import(NpgsqlConnection conn, ContentExport export)
    {
        using var tx = conn.BeginTransaction();
        try
        {
            // Clear every table that's either in the export or currently a content table in the target —
            // so a content type that existed in the target but isn't in this export (e.g. removed) still
            // ends up empty, matching "target becomes exactly what the export describes."
            var tablesToClear = new HashSet<string>(export.Tables.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var t in ListContentTables(conn, tx)) tablesToClear.Add(t);

            if (tablesToClear.Count > 0)
            {
                var list = string.Join(", ", new List<string>(tablesToClear).ConvertAll(t => $"\"{t}\""));
                using var cmd = new NpgsqlCommand($"TRUNCATE TABLE {list} CASCADE;", conn, tx);
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new NpgsqlCommand("SET session_replication_role = replica;", conn, tx))
                cmd.ExecuteNonQuery();

            foreach (var kv in export.Tables)
                InsertRows(conn, tx, kv.Key, kv.Value);

            using (var cmd = new NpgsqlCommand("SET session_replication_role = DEFAULT;", conn, tx))
                cmd.ExecuteNonQuery();

            foreach (var kv in export.Tables)
                ResetSerialSequences(conn, tx, kv.Key, kv.Value.Columns);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    static void InsertRows(NpgsqlConnection conn, NpgsqlTransaction tx, string table, TableExport data)
    {
        if (data.Rows.Count == 0) return;

        var colList = string.Join(", ", data.Columns.ConvertAll(c => $"\"{c.Name}\""));
        var paramList = string.Join(", ", data.Columns.ConvertAll(c => $"@{c.Name}"));
        var sql = $"INSERT INTO \"{table}\" ({colList}) VALUES ({paramList})";

        foreach (var row in data.Rows)
        {
            using var cmd = new NpgsqlCommand(sql, conn, tx);
            for (int i = 0; i < data.Columns.Count; i++)
            {
                var col = data.Columns[i];
                var value = row[i];
                var p = cmd.Parameters.Add($"@{col.Name}", MapDbType(col.PgType));
                p.Value = value == null ? DBNull.Value : (object)FromCanonicalString(value, col.PgType);
            }
            cmd.ExecuteNonQuery();
        }
    }

    // For every column that's backed by a serial/identity sequence, bump the sequence past the highest
    // imported id — otherwise the next API-driven INSERT would collide with an imported row.
    static void ResetSerialSequences(NpgsqlConnection conn, NpgsqlTransaction tx, string table, List<ColumnSpec> columns)
    {
        foreach (var col in columns)
        {
            using var seqCmd = new NpgsqlCommand("SELECT pg_get_serial_sequence(@t, @c)", conn, tx);
            seqCmd.Parameters.AddWithValue("t", table);
            seqCmd.Parameters.AddWithValue("c", col.Name);
            var seqName = seqCmd.ExecuteScalar() as string;
            if (string.IsNullOrEmpty(seqName)) continue;

            using var setCmd = new NpgsqlCommand(
                $"SELECT setval(@seq, COALESCE((SELECT MAX(\"{col.Name}\") FROM \"{table}\"), 0) + 1, false)", conn, tx);
            setCmd.Parameters.AddWithValue("seq", seqName);
            setCmd.ExecuteScalar();
        }
    }

    // ── Schema introspection ─────────────────────────────────────────────────────

    public static List<string> ListContentTables(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var tables = new List<string>();
        using var cmd = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name", conn, tx);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            if (!ExcludedTables.Contains(name)) tables.Add(name);
        }
        return tables;
    }

    static List<ColumnSpec> ListColumns(NpgsqlConnection conn, string table)
    {
        var columns = new List<ColumnSpec>();
        using var cmd = new NpgsqlCommand(
            "SELECT column_name, data_type FROM information_schema.columns " +
            "WHERE table_schema = 'public' AND table_name = @t ORDER BY ordinal_position", conn);
        cmd.Parameters.AddWithValue("t", table);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            columns.Add(new ColumnSpec { Name = reader.GetString(0), PgType = reader.GetString(1) });
        return columns;
    }

    // ── Canonical string <-> typed value ─────────────────────────────────────────

    static string ToCanonicalString(object value, string pgType) => pgType switch
    {
        "timestamp with time zone" => ((DateTime)value).ToString("O", CultureInfo.InvariantCulture),
        "real" or "double precision" => Convert.ToDouble(value).ToString("R", CultureInfo.InvariantCulture),
        "boolean" => ((bool)value) ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture),
    };

    static object FromCanonicalString(string s, string pgType) => pgType switch
    {
        "integer" => int.Parse(s, CultureInfo.InvariantCulture),
        "bigint" => long.Parse(s, CultureInfo.InvariantCulture),
        "real" or "double precision" => double.Parse(s, CultureInfo.InvariantCulture),
        "boolean" => bool.Parse(s),
        "timestamp with time zone" => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        _ => s,
    };

    static NpgsqlDbType MapDbType(string pgType) => pgType switch
    {
        "integer" => NpgsqlDbType.Integer,
        "bigint" => NpgsqlDbType.Bigint,
        "real" => NpgsqlDbType.Real,
        "double precision" => NpgsqlDbType.Double,
        "boolean" => NpgsqlDbType.Boolean,
        "timestamp with time zone" => NpgsqlDbType.TimestampTz,
        _ => NpgsqlDbType.Text,
    };
}
