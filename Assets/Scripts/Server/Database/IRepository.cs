using Npgsql;

/// <summary>
/// Marker for hand-written Npgsql repositories. Convention (no base machinery):
///   - writes: <c>Upsert(NpgsqlConnection conn, NpgsqlTransaction tx, TSnapshot snapshot)</c>
///   - reads:  <c>TSnapshot Load(NpgsqlConnection conn, &lt;id&gt;, NpgsqlTransaction tx = null)</c>
/// Parameterized commands only. Reads take an optional transaction so they work both inside a
/// worker job's tx and on a standalone load connection. Real entity repositories arrive in 1.3.
/// </summary>
public interface IRepository { }

/// <summary>Plain-data snapshot for the throwaway DAL self-test table.</summary>
public struct DalSmokeSnapshot
{
    public string Id;
    public string Payload;
}

/// <summary>Self-test repository over <c>dal_smoke</c>. Demonstrates the DAL pattern end to end.</summary>
public sealed class DalSmokeRepository : IRepository
{
    public void Upsert(NpgsqlConnection conn, NpgsqlTransaction tx, DalSmokeSnapshot s)
    {
        using var cmd = new NpgsqlCommand(
            "INSERT INTO dal_smoke (id, payload, updated_at) VALUES (@id, @payload, now()) " +
            "ON CONFLICT (id) DO UPDATE SET payload = EXCLUDED.payload, updated_at = now()",
            conn, tx);
        cmd.Parameters.AddWithValue("id", s.Id);
        cmd.Parameters.AddWithValue("payload", s.Payload);
        cmd.ExecuteNonQuery();
    }

    public string Load(NpgsqlConnection conn, string id, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand("SELECT payload FROM dal_smoke WHERE id = @id", conn, tx);
        cmd.Parameters.AddWithValue("id", id);
        return cmd.ExecuteScalar() as string;
    }

    public void Delete(NpgsqlConnection conn, string id, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand("DELETE FROM dal_smoke WHERE id = @id", conn, tx);
        cmd.Parameters.AddWithValue("id", id);
        cmd.ExecuteNonQuery();
    }
}
