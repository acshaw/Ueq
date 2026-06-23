using System;
using Npgsql;
using UnityEngine;

/// <summary>
/// Server-only entry point for the Postgres-backed persistence layer.
/// 1.1 scope: prove the server can connect, migrate, and seed at startup.
/// Repositories + the async save queue arrive in 1.2; until then this is just a
/// thin connection factory plus the startup routine.
/// </summary>
public static class Database
{
    static string _connectionString;

    /// <summary>Opens a fresh connection. The caller owns disposal (use <c>using</c>).</summary>
    public static NpgsqlConnection OpenConnection()
    {
        if (string.IsNullOrEmpty(_connectionString))
            _connectionString = DatabaseConfig.Resolve().ConnectionString;
        var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        return conn;
    }

    /// <summary>
    /// Connect, run any pending migrations, and seed (dev only). Throws on failure so the
    /// caller can abort server start rather than run silently without persistence.
    /// </summary>
    public static void InitializeServer()
    {
        var settings = DatabaseConfig.Resolve();
        _connectionString = settings.ConnectionString;

        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        Debug.Log($"[DB] Connected to {conn.DataSource}/{conn.Database}.");

        int applied = MigrationRunner.Run(conn);
        Debug.Log(applied == 0
            ? "[DB] Schema up to date (no pending migrations)."
            : $"[DB] Applied {applied} migration(s).");

        if (settings.SeedOnStart)
            DatabaseSeeder.Seed(conn);
    }

    /// <summary>Run a unit of work in a transaction on an existing connection (commit, else rollback).</summary>
    public static void RunInTransaction(NpgsqlConnection conn, Action<NpgsqlConnection, NpgsqlTransaction> body)
    {
        using var tx = conn.BeginTransaction();
        try
        {
            body(conn, tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>Open a connection and run a unit of work in a transaction on it.</summary>
    public static void RunInTransaction(Action<NpgsqlConnection, NpgsqlTransaction> body)
    {
        using var conn = OpenConnection();
        RunInTransaction(conn, body);
    }
}
