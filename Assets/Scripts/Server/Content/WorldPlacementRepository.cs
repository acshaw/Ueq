using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;

/// <summary>
/// DAL over the <c>world_placements</c> table (2.7.3, Stage A). Unlike most content repositories (read-only
/// — the web API owns writes), this one also writes: the Unity Editor's sync/import tools are the actual
/// authors of this content type, and per this project's own convention (<c>ContentExportImport.cs</c>, the
/// migration runner, the seeder) an Editor tool talks to Postgres directly via
/// <see cref="Database.OpenEditorConnection"/>, never through the ASP.NET API. <see cref="LoadAll"/> is the
/// server-startup read path (<c>ContentLoader</c>); <see cref="LoadForZone"/>/<see cref="Upsert"/>/
/// <see cref="Delete"/> are the Editor-tool read/write path.
/// </summary>
public sealed class WorldPlacementRepository : IRepository
{
    const string SelectColumns =
        "placement_id, zone_id, marker_type, pos_x, pos_y, pos_z, rot_y, data";

    public List<WorldPlacementSnapshot> LoadAll(NpgsqlConnection conn, NpgsqlTransaction tx = null)
    {
        var rows = new List<WorldPlacementSnapshot>();
        using var cmd = new NpgsqlCommand(
            $"SELECT {SelectColumns} FROM world_placements ORDER BY zone_id, placement_id", conn, tx);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add(ReadRow(reader));
        return rows;
    }

    public List<WorldPlacementSnapshot> LoadForZone(NpgsqlConnection conn, string zoneId, NpgsqlTransaction tx = null)
    {
        var rows = new List<WorldPlacementSnapshot>();
        using var cmd = new NpgsqlCommand(
            $"SELECT {SelectColumns} FROM world_placements WHERE zone_id = @zoneId ORDER BY placement_id", conn, tx);
        cmd.Parameters.AddWithValue("zoneId", zoneId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add(ReadRow(reader));
        return rows;
    }

    /// <summary>Insert or fully overwrite a row (export/sync always upserts, WP4).</summary>
    public void Upsert(NpgsqlConnection conn, WorldPlacementSnapshot row, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO world_placements (placement_id, zone_id, marker_type, pos_x, pos_y, pos_z, rot_y, data, updated_at)
            VALUES (@id, @zoneId, @markerType, @posX, @posY, @posZ, @rotY, @data, now())
            ON CONFLICT (placement_id) DO UPDATE SET
                zone_id = EXCLUDED.zone_id,
                marker_type = EXCLUDED.marker_type,
                pos_x = EXCLUDED.pos_x,
                pos_y = EXCLUDED.pos_y,
                pos_z = EXCLUDED.pos_z,
                rot_y = EXCLUDED.rot_y,
                data = EXCLUDED.data,
                updated_at = now()", conn, tx);

        cmd.Parameters.AddWithValue("id", new System.Guid(row.PlacementId));
        cmd.Parameters.AddWithValue("zoneId", row.ZoneId);
        cmd.Parameters.AddWithValue("markerType", row.MarkerType);
        cmd.Parameters.AddWithValue("posX", (object)row.PosX ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("posY", (object)row.PosY ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("posZ", (object)row.PosZ ?? System.DBNull.Value);
        cmd.Parameters.AddWithValue("rotY", row.RotY);
        var dataParam = cmd.Parameters.Add("data", NpgsqlDbType.Jsonb);
        dataParam.Value = row.Data;

        cmd.ExecuteNonQuery();
    }

    /// <summary>Explicit removal only — never called automatically by sync (WP4).</summary>
    public void Delete(NpgsqlConnection conn, string placementId, NpgsqlTransaction tx = null)
    {
        using var cmd = new NpgsqlCommand("DELETE FROM world_placements WHERE placement_id = @id", conn, tx);
        cmd.Parameters.AddWithValue("id", new System.Guid(placementId));
        cmd.ExecuteNonQuery();
    }

    static WorldPlacementSnapshot ReadRow(NpgsqlDataReader reader) => new()
    {
        PlacementId = reader.GetGuid(0).ToString(),
        ZoneId      = reader.GetString(1),
        MarkerType  = reader.GetString(2),
        PosX        = reader.IsDBNull(3) ? null : reader.GetFloat(3),
        PosY        = reader.IsDBNull(4) ? null : reader.GetFloat(4),
        PosZ        = reader.IsDBNull(5) ? null : reader.GetFloat(5),
        RotY        = reader.GetFloat(6),
        Data        = reader.GetString(7),
    };
}
