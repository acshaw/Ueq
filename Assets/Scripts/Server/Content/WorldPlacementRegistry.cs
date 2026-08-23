using System.Collections.Generic;

/// <summary>
/// Server-only lookup of world placements by zone (2.7.3, Stage A). Populated by <c>ContentLoader</c> at
/// startup; consumed by <see cref="ZoneManager"/>'s materialize-if-missing + refresh-if-present step when
/// each zone scene is registered.
/// </summary>
public static class WorldPlacementRegistry
{
    static readonly Dictionary<string, List<WorldPlacementSnapshot>> _byZone = new();

    public static void LoadFrom(IEnumerable<WorldPlacementSnapshot> snapshots)
    {
        _byZone.Clear();
        foreach (var s in snapshots)
        {
            if (string.IsNullOrEmpty(s.ZoneId)) continue;
            if (!_byZone.TryGetValue(s.ZoneId, out var list)) { list = new List<WorldPlacementSnapshot>(); _byZone[s.ZoneId] = list; }
            list.Add(s);
        }
    }

    public static IReadOnlyList<WorldPlacementSnapshot> ForZone(string zoneId)
        => _byZone.TryGetValue(zoneId, out var list) ? list : System.Array.Empty<WorldPlacementSnapshot>();

    public static int Count
    {
        get
        {
            int total = 0;
            foreach (var list in _byZone.Values) total += list.Count;
            return total;
        }
    }
}
