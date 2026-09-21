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

    /// <summary>8.6 — whether this marker type implements <see cref="IReferencesOtherPlacements"/>, so
    /// <see cref="PlacementMaterializer.SplitPasses"/> can route it to pass 2 generically. Previously
    /// hardcoded to a literal "SpawnPoint" string check in SplitPasses — that silently broke the moment a
    /// second referencing type (PopulationZone) was added, since its rows would land in pass 1 and
    /// ResolveReferences (only ever called for pass-2 rows) would never run for it. Fixed here instead of
    /// patched with another hardcoded name, so the next referencing type needs no PlacementMaterializer
    /// edit either — genuinely delivering on this file's own "no other code needs to change" claim (WP1).</summary>
    bool IsReferencer { get; }
}

public sealed class SpawnPointPlacementFactory : IPlacementFactory
{
    public IWorldPlacement AddTo(GameObject go) => go.AddComponent<SpawnPoint>();
    public bool IsReferencer => true; // references patrolRoute/wanderRegion/pool
}

public sealed class PatrolRoutePlacementFactory : IPlacementFactory
{
    public IWorldPlacement AddTo(GameObject go) => go.AddComponent<PatrolRoute>();
    public bool IsReferencer => false;
}

public sealed class WanderRegionPlacementFactory : IPlacementFactory
{
    public IWorldPlacement AddTo(GameObject go) => go.AddComponent<WanderRegion>();
    public bool IsReferencer => false;
}

public sealed class SpawnPoolPlacementFactory : IPlacementFactory
{
    public IWorldPlacement AddTo(GameObject go) => go.AddComponent<SpawnPool>();
    public bool IsReferencer => false;
}

public sealed class PopulationZonePlacementFactory : IPlacementFactory
{
    public IWorldPlacement AddTo(GameObject go) => go.AddComponent<PopulationZone>();
    public bool IsReferencer => true; // references a WanderRegion (8.6, PZ2)
}

/// <summary>Adding a future marker type = one new component + one new factory + one line registering it
/// here — no other code in the sync/import/materialize pipeline needs to change (WP1), now genuinely true
/// for referencing types too (see IPlacementFactory.IsReferencer's doc comment for the bug this fixed).</summary>
public static class PlacementFactoryRegistry
{
    static readonly Dictionary<string, IPlacementFactory> _byType = new()
    {
        ["SpawnPoint"]      = new SpawnPointPlacementFactory(),
        ["PatrolRoute"]     = new PatrolRoutePlacementFactory(),
        ["WanderRegion"]    = new WanderRegionPlacementFactory(),
        ["SpawnPool"]       = new SpawnPoolPlacementFactory(),
        ["PopulationZone"]  = new PopulationZonePlacementFactory(),
    };

    public static IPlacementFactory Get(string markerType)
        => _byType.TryGetValue(markerType, out var f) ? f : null;
}
