using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Npgsql;
using UnityEngine;

/// <summary>
/// Applies versioned .sql migrations from StreamingAssets/Database/Migrations.
/// Files are named <c>NNNN_description.sql</c>; the leading integer is the version.
/// Each unapplied file runs in its own transaction and is recorded in
/// <c>schema_version</c>. Idempotent: re-running applies nothing.
/// </summary>
public static class MigrationRunner
{
    public static string MigrationsDir =>
        Path.Combine(Application.streamingAssetsPath, "Database", "Migrations");

    /// <returns>Number of migrations applied during this call.</returns>
    public static int Run(NpgsqlConnection conn)
    {
        EnsureLedger(conn);
        var applied = LoadAppliedVersions(conn);
        var pending = DiscoverMigrations()
            .Where(m => !applied.Contains(m.version))
            .OrderBy(m => m.version)
            .ToList();

        foreach (var m in pending)
        {
            var sql = File.ReadAllText(m.path);
            using var tx = conn.BeginTransaction();
            try
            {
                using (var cmd = new NpgsqlCommand(sql, conn, tx))
                    cmd.ExecuteNonQuery();
                using (var record = new NpgsqlCommand(
                    "INSERT INTO schema_version (version, name) VALUES (@v, @n)", conn, tx))
                {
                    record.Parameters.AddWithValue("v", m.version);
                    record.Parameters.AddWithValue("n", m.name);
                    record.ExecuteNonQuery();
                }
                tx.Commit();
                Debug.Log($"[DB] Applied migration {m.version:0000} — {m.name}");
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
        return pending.Count;
    }

    static void EnsureLedger(NpgsqlConnection conn)
    {
        // Bootstrap so we can read applied versions on a fresh DB. 0001_init.sql
        // re-declares this with IF NOT EXISTS so the ledger is self-documenting too.
        const string sql =
            "CREATE TABLE IF NOT EXISTS schema_version (" +
            "version INTEGER PRIMARY KEY, " +
            "name TEXT NOT NULL, " +
            "applied_at TIMESTAMPTZ NOT NULL DEFAULT now())";
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    static HashSet<int> LoadAppliedVersions(NpgsqlConnection conn)
    {
        var set = new HashSet<int>();
        using var cmd = new NpgsqlCommand("SELECT version FROM schema_version", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            set.Add(reader.GetInt32(0));
        return set;
    }

    static IEnumerable<(int version, string name, string path)> DiscoverMigrations()
    {
        if (!Directory.Exists(MigrationsDir))
            yield break;

        foreach (var path in Directory.GetFiles(MigrationsDir, "*.sql"))
        {
            // Guard against Windows GetFiles quirks (e.g. matching .sql.meta).
            if (!path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                continue;

            var file = Path.GetFileNameWithoutExtension(path);
            var underscore = file.IndexOf('_');
            var versionStr = underscore > 0 ? file.Substring(0, underscore) : file;
            if (int.TryParse(versionStr, out var version))
                yield return (version, file, path);
            else
                Debug.LogWarning($"[DB] Skipping migration with unparsable version: {file}");
        }
    }
}
