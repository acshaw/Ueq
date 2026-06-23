using Npgsql;

/// <summary>
/// Hand-written Npgsql repository over <c>accounts</c> (1.4). Follows the 1.2 convention:
/// parameterized commands only; reads take an optional transaction. Usernames are expected
/// already-normalized (trimmed + lower-cased) by the caller.
/// </summary>
public sealed class AccountRepository : IRepository
{
    /// <summary>
    /// Register if the username is free. Atomic via INSERT … ON CONFLICT DO NOTHING RETURNING:
    /// returns the new account id, or <c>null</c> if the username was already taken.
    /// </summary>
    public long? TryRegister(NpgsqlConnection conn, string username, string passwordHash, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO accounts (username, password_hash) VALUES (@u, @h) " +
            "ON CONFLICT (username) DO NOTHING RETURNING account_id", conn, tx);
        cmd.Parameters.AddWithValue("u", username);
        cmd.Parameters.AddWithValue("h", passwordHash);
        var result = cmd.ExecuteScalar();
        return (result == null || result is System.DBNull) ? (long?)null : (long)result;
    }

    /// <summary>Look up an account's id + stored hash by username, or <c>null</c> if not found.</summary>
    public (long id, string hash)? FindByUsername(NpgsqlConnection conn, string username, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand(
            "SELECT account_id, password_hash FROM accounts WHERE username = @u", conn, tx);
        cmd.Parameters.AddWithValue("u", username);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return (reader.GetInt64(0), reader.GetString(1));
    }

    /// <summary>Stamp last_login_at = now() for a successful login.</summary>
    public void TouchLogin(NpgsqlConnection conn, long accountId, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand(
            "UPDATE accounts SET last_login_at = now() WHERE account_id = @id", conn, tx);
        cmd.Parameters.AddWithValue("id", accountId);
        cmd.ExecuteNonQuery();
    }
}
