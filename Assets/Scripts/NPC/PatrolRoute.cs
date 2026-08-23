using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 3.1.10 Stage 1 — a patrol route authored in the scene: drop ordered empty children under this object and
/// they become the waypoints (same child-transform convention as the trail tools). A <c>SpawnPoint</c> that
/// references this route seeds each spawned mob's <see cref="PatrolBehavior"/> with the world-space points, so
/// the mob walks the beat instead of random-wandering. Scene-view labels for each point come with the Stage 2
/// placement tooling; this draws the path + waypoint spheres.
/// </summary>
public class PatrolRoute : MonoBehaviour, IWorldPlacement
{
    [Header("World Placement Sync (2.7.3)")]
    [Tooltip("GUID assigned once when this object is placed. Never hand-edit.")]
    [SerializeField] string placementId = "";

    [Tooltip("Loop back to the first point (true) or ping-pong back along the route (false).")]
    public bool  loop = true;

    [Tooltip("Seconds a mob pauses at each waypoint before moving on.")]
    public float pausePerPoint = 2f;

    public bool HasPoints => transform.childCount > 0;

    /// <summary>World-space positions of the ordered child waypoints.</summary>
    public Vector3[] Points
    {
        get
        {
            var pts = new Vector3[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                pts[i] = transform.GetChild(i).position;
            return pts;
        }
    }

    // ── World Placement Sync (2.7.3, Stage A) ──────────────────────────────────

    public string PlacementId => placementId;
    public string MarkerType  => "PatrolRoute";
    public void   SetPlacementId(string id) => placementId = id;

    public JObject CapturePlacementData()
    {
        var points = new JArray();
        foreach (var p in Points)
            points.Add(new JObject { ["x"] = p.x, ["y"] = p.y, ["z"] = p.z });
        return new JObject { ["loop"] = loop, ["pausePerPoint"] = pausePerPoint, ["points"] = points };
    }

    // Waypoints are spatial, not config (same reasoning as SpawnPoint's position): an already-placed route
    // (this object already has child waypoints) keeps its own scene-authored points untouched on refresh —
    // only loop/pausePerPoint refresh. A freshly materialized route (zero children — it was just created by
    // the placement factory) has no other source for its shape, so its waypoints are built from the data.
    public void ApplyPlacementData(JObject data)
    {
        loop          = (bool?)data["loop"] ?? loop;
        pausePerPoint = (float?)data["pausePerPoint"] ?? pausePerPoint;

        if (transform.childCount > 0) return;
        if (data["points"] is not JArray points) return;
        foreach (var p in points)
        {
            var wp = new GameObject($"WP {transform.childCount}");
            wp.transform.SetParent(transform, worldPositionStays: false);
            wp.transform.position = new Vector3((float)p["x"], (float)p["y"], (float)p["z"]);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(placementId))
            placementId = System.Guid.NewGuid().ToString();
    }
#endif

    void OnDrawGizmos()
    {
        int n = transform.childCount;
        if (n == 0) return;

        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        for (int i = 0; i < n; i++)
        {
            var p = transform.GetChild(i).position;
            Gizmos.DrawWireSphere(p, 0.5f);
            if (i > 0) Gizmos.DrawLine(transform.GetChild(i - 1).position, p);
        }
        if (loop && n > 1)
            Gizmos.DrawLine(transform.GetChild(n - 1).position, transform.GetChild(0).position);
    }
}
