using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2.7.3 (Stage A) — creates a bare, unconfigured marker component for a given <c>marker_type</c>. Actual
/// configuration happens afterward via <see cref="IWorldPlacement.ApplyPlacementData"/> — the factory's
/// only job is "which component gets added." Used by both the runtime materialize-if-missing path
/// (<see cref="ZoneManager"/>) and the Editor import tool (Stage B), so a placement looks identical
/// regardless of which one created it.
/// </summary>
public interface IPlacementFactory
{
    IWorldPlacement AddTo(GameObject go);
}

public sealed class SpawnPointPlacementFactory : IPlacementFactory
{
    public IWorldPlacement AddTo(GameObject go) => go.AddComponent<SpawnPoint>();
}

public sealed class PatrolRoutePlacementFactory : IPlacementFactory
{
    public IWorldPlacement AddTo(GameObject go) => go.AddComponent<PatrolRoute>();
}

public sealed class WanderRegionPlacementFactory : IPlacementFactory
{
    public IWorldPlacement AddTo(GameObject go) => go.AddComponent<WanderRegion>();
}

/// <summary>Adding a future marker type = one new component + one new factory + one line registering it
/// here — no other code in the sync/import/materialize pipeline needs to change (WP1).</summary>
public static class PlacementFactoryRegistry
{
    static readonly Dictionary<string, IPlacementFactory> _byType = new()
    {
        ["SpawnPoint"]   = new SpawnPointPlacementFactory(),
        ["PatrolRoute"]  = new PatrolRoutePlacementFactory(),
        ["WanderRegion"] = new WanderRegionPlacementFactory(),
    };

    public static IPlacementFactory Get(string markerType)
        => _byType.TryGetValue(markerType, out var f) ? f : null;
}
