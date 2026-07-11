using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 3.1.11 — a mob's "where may I roam while idle" source. <see cref="WanderBehavior"/> samples a region for its
/// next destination, so the same wander loop serves three authoring modes: leashed to spawn, bounded to an
/// authored area, or free-range across the zone. Regions never constrain chase/aggro — only idle wander.
/// </summary>
public interface IWanderRegion
{
    /// <summary>A random navmesh point inside the region, or false if the sample missed (retry next tick).</summary>
    bool TryGetRandomPoint(out Vector3 point);

    /// <summary>Where the mob should go when it disengages (WR5): spawn (leash → walk home + heal) for
    /// spawn-anchored regions, or the current position (reset in place) for roaming ones.</summary>
    Vector3 GetReturnAnchor(Vector3 currentPos);
}

/// <summary>Leash mode (default, unchanged from pre-3.1.11): sample a sphere of <c>radius</c> around the spawn
/// point. Byte-for-byte the old <c>WanderBehavior.RandomNavPoint</c> behavior.</summary>
public class SpawnAnchoredRegion : IWanderRegion
{
    readonly Vector3 _spawn;
    readonly float   _radius;

    public SpawnAnchoredRegion(Vector3 spawn, float radius) { _spawn = spawn; _radius = radius; }

    public bool TryGetRandomPoint(out Vector3 point)
    {
        var candidate = _spawn + Random.insideUnitSphere * _radius;
        candidate.y   = _spawn.y;
        if (NavMesh.SamplePosition(candidate, out var hit, _radius, NavMesh.AllAreas)) { point = hit.position; return true; }
        point = default; return false;
    }

    public Vector3 GetReturnAnchor(Vector3 currentPos) => _spawn; // walk home
}

/// <summary>Bounded mode: sample an authored box/sphere volume placed in the scene, ignoring the spawn point.</summary>
public class BoundedRegion : IWanderRegion
{
    readonly WanderRegion _region;

    public BoundedRegion(WanderRegion region) { _region = region; }

    public bool TryGetRandomPoint(out Vector3 point)
    {
        if (_region == null) { point = default; return false; } // volume destroyed
        var candidate = _region.RandomPointInVolume();
        if (NavMesh.SamplePosition(candidate, out var hit, _region.SampleRadius, NavMesh.AllAreas)) { point = hit.position; return true; }
        point = default; return false;
    }

    public Vector3 GetReturnAnchor(Vector3 currentPos) => currentPos; // reset in place
}

/// <summary>Free-range mode: roam the whole zone. Samples a large spread around an anchor but snaps a candidate
/// onto the navmesh only within a small distance — so a point can never snap across the ~5000u zone-offset gap
/// to another zone's navmesh island (WR3). Off-navmesh candidates just fail and retry.</summary>
public class ZoneRegion : IWanderRegion
{
    readonly Vector3 _center;
    readonly float   _radius;
    const    float   SnapDistance = 6f;

    public ZoneRegion(Vector3 center, float radius) { _center = center; _radius = radius; }

    public bool TryGetRandomPoint(out Vector3 point)
    {
        var candidate = _center + Random.insideUnitSphere * _radius;
        candidate.y   = _center.y;
        if (NavMesh.SamplePosition(candidate, out var hit, SnapDistance, NavMesh.AllAreas)) { point = hit.position; return true; }
        point = default; return false;
    }

    public Vector3 GetReturnAnchor(Vector3 currentPos) => currentPos; // reset in place
}
