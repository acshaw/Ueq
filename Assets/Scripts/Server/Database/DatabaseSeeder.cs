using Npgsql;
using UnityEngine;

/// <summary>
/// Dev-only seed hook, run after migrations when seedOnStart is enabled.
/// Content/seed data (reference rows, dev fixtures) plugs in here as the schema grows.
/// </summary>
public static class DatabaseSeeder
{
    // Host-mode convenience account (1.4 / decision O5) — the dev login auto-fills these.
    public const string DevUsername = "dev";
    public const string DevPassword = "devpass";

    public static void Seed(NpgsqlConnection conn)
    {
        SeedDevAccount(conn);
    }

    // Idempotent: creates the dev account once so Host testing is one click.
    static void SeedDevAccount(NpgsqlConnection conn)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO accounts (username, password_hash) VALUES (@u, @h) " +
            "ON CONFLICT (username) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("u", DevUsername);
        cmd.Parameters.AddWithValue("h", PasswordHasher.Hash(DevPassword));
        int rows = cmd.ExecuteNonQuery();
        Debug.Log(rows > 0
            ? $"[DB] Seed: created dev account '{DevUsername}'."
            : "[DB] Seed: dev account already present.");
    }
}
