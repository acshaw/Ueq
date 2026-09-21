using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 3.1.11 — an authored roam area placed in the scene (box, sphere, or — 8.6 — an irregular polygon). A
/// <see cref="SpawnPoint"/> or <see cref="PopulationZone"/> that references this constrains its wander mobs
/// to points inside the volume instead of a sphere around the spawn point. Not networked — read server-side
/// at spawn to configure <see cref="WanderBehavior"/>, exactly like a <see cref="PatrolRoute"/>. Placement
/// tooling + Scene-view label come with the encounter tools.
/// </summary>
public class WanderRegion : MonoBehaviour, IWorldPlacement
{
    [Header("World Placement Sync (2.7.3)")]
    [Tooltip("GUID assigned once when this object is placed. Never hand-edit.")]
    [SerializeField] string placementId = "";

    public enum Shape { Box, Sphere, Polygon }

    [Tooltip("Box = a rectangular footprint (X/Z); Sphere = a radial area; Polygon (8.6) = an irregular " +
             "boundary defined by this object's ordered child transforms (at least 3), the same " +
             "child-transform convention PatrolRoute uses for waypoints. Y is not used for sampling (mobs " +
             "snap to the navmesh).")]
    public Shape shape = Shape.Box;

    [Tooltip("Box footprint size in local units (X and Z are the roam extent; Y is only for the gizmo).")]
    public Vector3 boxSize = new Vector3(30f, 4f, 30f);

    [Tooltip("Sphere roam radius in world units.")]
    public float sphereRadius = 15f;

    [Tooltip("How far a sampled point may snap onto the navmesh.")]
    public float sampleRadius = 4f;

    // 8.6 (PZ1) — bounds a degenerate/self-intersecting polygon's rejection-sampling loop instead of
    // spinning forever; falls back to the polygon's centroid if every attempt misses.
    const int MaxPolygonSampleAttempts = 30;

    public float SampleRadius => sampleRadius;

    /// <summary>8.6 — world-space positions of the ordered child vertices, for Polygon shape. Same
    /// convention as <c>PatrolRoute.Points</c>.</summary>
    public Vector3[] PolygonVertices
    {
        get
        {
            var pts = new Vector3[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                pts[i] = transform.GetChild(i).position;
            return pts;
        }
    }

    /// <summary>A random point inside the volume (world space); Y is the region's Y — the caller snaps to navmesh.</summary>
    public Vector3 RandomPointInVolume()
    {
        if (shape == Shape.Sphere)
        {
            var p = Random.insideUnitSphere * sphereRadius;
            return new Vector3(transform.position.x + p.x, transform.position.y, transform.position.z + p.z);
        }

        if (shape == Shape.Polygon)
            return RandomPointInPolygon();

        // Box: uniform within the local footprint, rotated/positioned by the transform.
        var local = new Vector3((Random.value - 0.5f) * boxSize.x, 0f, (Random.value - 0.5f) * boxSize.z);
        var world = transform.TransformPoint(local);
        world.y   = transform.position.y;
        return world;
    }

    // 8.6 (PZ1) — rejection sampling within the polygon's XZ bounding box + a point-in-polygon test. Chosen
    // over triangulation-fan sampling (uniform-by-area, more "correct" statistically) — ear-clipping a
    // possibly-non-convex natural boundary is real geometry work for a difference nobody perceives at
    // sub-30-vertex, sub-10-population scale.
    Vector3 RandomPointInPolygon()
    {
        var verts = PolygonVertices;
        if (verts.Length < 3) return transform.position; // degenerate — not enough vertices to form an area

        float minX = verts[0].x, maxX = verts[0].x, minZ = verts[0].z, maxZ = verts[0].z;
        foreach (var v in verts)
        {
            if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
            if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
        }

        for (int i = 0; i < MaxPolygonSampleAttempts; i++)
        {
            float x = Random.Range(minX, maxX);
            float z = Random.Range(minZ, maxZ);
            if (IsPointInPolygon(verts, x, z))
                return new Vector3(x, verts[0].y, z);
        }

        Debug.LogWarning($"[WanderRegion] {name}: polygon sampling failed after {MaxPolygonSampleAttempts} " +
                         "attempts (degenerate or self-intersecting shape?) — falling back to the centroid.", this);
        return PolygonCentroid(verts);
    }

    // Ray-casting / even-odd rule on the XZ plane — correct regardless of convexity (a natural boundary is
    // plausibly non-convex), not robust to a genuinely self-intersecting polygon (an authoring mistake, not
    // a case this needs to handle gracefully beyond not looping forever, per the capped retry above).
    static bool IsPointInPolygon(Vector3[] verts, float x, float z)
    {
        bool inside = false;
        int n = verts.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = verts[i].x, zi = verts[i].z;
            float xj = verts[j].x, zj = verts[j].z;
            bool crosses = (zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi;
            if (crosses) inside = !inside;
        }
        return inside;
    }

    static Vector3 PolygonCentroid(Vector3[] verts)
    {
        Vector3 sum = Vector3.zero;
        foreach (var v in verts) sum += v;
        return sum / verts.Length;
    }

    void OnDrawGizmos()
    {
        if (shape == Shape.Polygon)
        {
            var verts = PolygonVertices;
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            for (int i = 0; i < verts.Length; i++)
            {
                Gizmos.DrawWireSphere(verts[i], 0.5f);
                if (i > 0) Gizmos.DrawLine(verts[i - 1], verts[i]);
            }
            if (verts.Length > 2)
                Gizmos.DrawLine(verts[verts.Length - 1], verts[0]); // close the loop
            return;
        }

        Gizmos.color  = new Color(0.3f, 1f, 0.5f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (shape == Shape.Sphere) Gizmos.DrawWireSphere(Vector3.zero, sphereRadius);
        else                       Gizmos.DrawWireCube(Vector3.zero, boxSize);
        Gizmos.matrix = Matrix4x4.identity;
    }

    // ── World Placement Sync (2.7.3, Stage A) ──────────────────────────────────
    // Shape/size are scalar config (same category as SpawnPoint.activationRadius), not spatial hierarchy —
    // they always refresh from the DB (WP5). Only this object's own anchor transform.position, and (8.6)
    // the Polygon shape's child vertices, are scene-owned/positional — set by the caller outside
    // ApplyPlacementData (anchor), or built once from the data only if this object has no children yet
    // (vertices), same convention PatrolRoute already established for its waypoints.

    public string PlacementId => placementId;
    public string MarkerType  => "WanderRegion";
    public void   SetPlacementId(string id) => placementId = id;

    public JObject CapturePlacementData()
    {
        var verts = new JArray();
        foreach (var p in PolygonVertices)
            verts.Add(new JObject { ["x"] = p.x, ["y"] = p.y, ["z"] = p.z });

        return new JObject
        {
            ["shape"]           = shape.ToString(),
            ["boxSize"]         = new JObject { ["x"] = boxSize.x, ["y"] = boxSize.y, ["z"] = boxSize.z },
            ["sphereRadius"]    = sphereRadius,
            ["sampleRadius"]    = sampleRadius,
            ["polygonVertices"] = verts, // 8.6
        };
    }

    public void ApplyPlacementData(JObject data)
    {
        if (data["shape"] != null && System.Enum.TryParse<Shape>((string)data["shape"], out var s))
            shape = s;
        if (data["boxSize"] is JObject box)
            boxSize = new Vector3((float)box["x"], (float)box["y"], (float)box["z"]);
        sphereRadius = (float?)data["sphereRadius"] ?? sphereRadius;
        sampleRadius = (float?)data["sampleRadius"] ?? sampleRadius;

        // 8.6: polygon vertices are spatial, not config (same reasoning as PatrolRoute's waypoints) — an
        // already-placed region (already has child vertices) keeps its own scene-authored points untouched
        // on refresh; a freshly materialized one (zero children) builds them from the data.
        if (transform.childCount == 0 && data["polygonVertices"] is JArray verts)
        {
            foreach (var v in verts)
            {
                var vtx = new GameObject($"Vertex {transform.childCount}");
                vtx.transform.SetParent(transform, worldPositionStays: false);
                vtx.transform.position = new Vector3((float)v["x"], (float)v["y"], (float)v["z"]);
            }
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(placementId))
            placementId = System.Guid.NewGuid().ToString();
    }
#endif
}
