using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 3.1.11 — an authored roam area placed in the scene (box or sphere). A <see cref="SpawnPoint"/> that references
/// this constrains its wander mobs to points inside the volume instead of a sphere around the spawn point. Not
/// networked — read server-side at spawn to configure <see cref="WanderBehavior"/>, exactly like a
/// <see cref="PatrolRoute"/>. Placement tooling + Scene-view label come with the encounter tools.
/// </summary>
public class WanderRegion : MonoBehaviour, IWorldPlacement
{
    [Header("World Placement Sync (2.7.3)")]
    [Tooltip("GUID assigned once when this object is placed. Never hand-edit.")]
    [SerializeField] string placementId = "";

    public enum Shape { Box, Sphere }

    [Tooltip("Box = a rectangular footprint (X/Z); Sphere = a radial area. Y is not used for sampling (mobs snap " +
             "to the navmesh).")]
    public Shape shape = Shape.Box;

    [Tooltip("Box footprint size in local units (X and Z are the roam extent; Y is only for the gizmo).")]
    public Vector3 boxSize = new Vector3(30f, 4f, 30f);

    [Tooltip("Sphere roam radius in world units.")]
    public float sphereRadius = 15f;

    [Tooltip("How far a sampled point may snap onto the navmesh.")]
    public float sampleRadius = 4f;

    public float SampleRadius => sampleRadius;

    /// <summary>A random point inside the volume (world space); Y is the region's Y — the caller snaps to navmesh.</summary>
    public Vector3 RandomPointInVolume()
    {
        if (shape == Shape.Sphere)
        {
            var p = Random.insideUnitSphere * sphereRadius;
            return new Vector3(transform.position.x + p.x, transform.position.y, transform.position.z + p.z);
        }

        // Box: uniform within the local footprint, rotated/positioned by the transform.
        var local = new Vector3((Random.value - 0.5f) * boxSize.x, 0f, (Random.value - 0.5f) * boxSize.z);
        var world = transform.TransformPoint(local);
        world.y   = transform.position.y;
        return world;
    }

    void OnDrawGizmos()
    {
        Gizmos.color  = new Color(0.3f, 1f, 0.5f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (shape == Shape.Sphere) Gizmos.DrawWireSphere(Vector3.zero, sphereRadius);
        else                       Gizmos.DrawWireCube(Vector3.zero, boxSize);
        Gizmos.matrix = Matrix4x4.identity;
    }

    // ── World Placement Sync (2.7.3, Stage A) ──────────────────────────────────
    // Shape/size are scalar config (same category as SpawnPoint.activationRadius), not spatial hierarchy —
    // they always refresh from the DB (WP5). Only this object's own anchor transform.position is
    // scene-owned/positional, and that's set by the caller outside ApplyPlacementData, same as every
    // other marker type.

    public string PlacementId => placementId;
    public string MarkerType  => "WanderRegion";
    public void   SetPlacementId(string id) => placementId = id;

    public JObject CapturePlacementData() => new()
    {
        ["shape"]        = shape.ToString(),
        ["boxSize"]      = new JObject { ["x"] = boxSize.x, ["y"] = boxSize.y, ["z"] = boxSize.z },
        ["sphereRadius"] = sphereRadius,
        ["sampleRadius"] = sampleRadius,
    };

    public void ApplyPlacementData(JObject data)
    {
        if (data["shape"] != null && System.Enum.TryParse<Shape>((string)data["shape"], out var s))
            shape = s;
        if (data["boxSize"] is JObject box)
            boxSize = new Vector3((float)box["x"], (float)box["y"], (float)box["z"]);
        sphereRadius = (float?)data["sphereRadius"] ?? sphereRadius;
        sampleRadius = (float?)data["sampleRadius"] ?? sampleRadius;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(placementId))
            placementId = System.Guid.NewGuid().ToString();
    }
#endif
}
