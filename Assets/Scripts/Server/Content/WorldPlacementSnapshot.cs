/// <summary>
/// Plain-data view of one <c>world_placements</c> row (2.7.3, Stage A). <c>Data</c> is the raw JSON text
/// from the <c>data</c> jsonb column — parsed to a <c>JObject</c> only where it's actually consumed
/// (<see cref="IWorldPlacement.ApplyPlacementData"/>), so this struct stays free of any JSON-library
/// dependency, matching <see cref="ItemSnapshot"/>'s "plain data only" convention.
/// </summary>
public struct WorldPlacementSnapshot
{
    public string PlacementId;
    public string ZoneId;
    public string MarkerType;

    public float? PosX, PosY, PosZ;
    public float  RotY;

    public string Data; // raw JSON text
}
