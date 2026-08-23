namespace Ueq.ContentApi.Models;

/// <summary>
/// EF entity mapping onto the <c>world_placements</c> table (2.7.3). Mirrors the migration columns 1:1
/// (and the Unity <c>WorldPlacementSnapshot</c>). Mapping-only — the SQL runner owns the schema.
///
/// Unlike every other content type, the web API is <b>not</b> where this content is authored — Unity's
/// sync/import Editor tools write these rows directly via Npgsql (devplan WP4/WP7). This API exists only
/// for the web Placement Editor's narrower job: viewing every placement and editing a <c>SpawnPoint</c>'s
/// non-spatial config (<c>Data</c>) without reopening Unity. <c>ZoneId</c>/<c>MarkerType</c>/position/
/// rotation are Unity-authored and never web-editable (WP7) — <see cref="Controllers.WorldPlacementsController.Update"/>
/// only ever applies changes to <c>Data</c>.
/// </summary>
public class WorldPlacement
{
    public Guid PlacementId { get; set; }
    public string ZoneId { get; set; } = string.Empty;
    public string MarkerType { get; set; } = string.Empty;

    public float? PosX { get; set; }
    public float? PosY { get; set; }
    public float? PosZ { get; set; }
    public float RotY { get; set; }

    /// <summary>Raw JSON text (maps onto the <c>jsonb</c> column) — marker-type-specific config. The web
    /// editor parses/re-serializes this client-side rather than the API modeling every marker type's shape.</summary>
    public string Data { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; }
}
