using System.Collections.Generic;
using Npgsql;

/// <summary>
/// Plain-data snapshot for one <c>content_ping</c> row. Throwaway smoke type (2.1) —
/// proves Angular → API → Postgres → Unity loads. Replaced by real content snapshots in 2.2.
/// </summary>
public struct ContentPingSnapshot
{
    public long   Id;
    public string Label;
}

/// <summary>
/// Read-only repository over <c>content_ping</c>, following the 1.2 DAL convention
/// (hand-written Npgsql, parameterized, reads take an optional transaction). Content
/// repositories are read-mostly: the web API owns writes; the game server only loads.
/// </summary>
public sealed class ContentPingRepository : IRepository
{
    public List<ContentPingSnapshot> LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var rows = new List<ContentPingSnapshot>();
        using var cmd = new NpgsqlCommand(
            "SELECT id, label FROM content_ping ORDER BY id", conn, tx);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add(new ContentPingSnapshot { Id = reader.GetInt64(0), Label = reader.GetString(1) });
        return rows;
    }
}
