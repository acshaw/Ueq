using UnityEngine;

/// <summary>
/// M3.0 (Z4) — an in-scene proximity portal. The server-side <c>ZoneManager</c> polls every portal each
/// tick; a player within <see cref="radius"/> is moved to <see cref="targetZoneId"/> and warped to the
/// entry named <see cref="targetEntryId"/> in that zone. Deliberately NOT a physics trigger: the 2.0
/// spike found a kinematic body stops firing <c>OnTriggerEnter</c> after a cross-scene move.
///
/// Place one at each zone boundary; its world position comes free from the transform. Lives inside its
/// owning zone scene (so it's authored at that zone's world offset).
/// </summary>
public class ZonePortal : MonoBehaviour
{
    [Tooltip("Zone id this portal sends players to.")]
    public string targetZoneId = "";

    [Tooltip("ZoneEntry.entryId in the destination zone to arrive at.")]
    public string targetEntryId = "default";

    [Tooltip("Server-side activation radius (proximity poll, horizontal). Keep small.")]
    public float radius = 2.5f;

    [Tooltip("Spawn a visible cyan pillar at runtime so players can see the portal (the trigger itself " +
             "is an invisible server-side proximity check). Turn off once real portal art exists.")]
    public bool showMarker = true;

    // A cosmetic, non-networked, collider-less pillar so the portal is findable in-game. Each peer
    // creates its own; it never blocks movement.
    void Start()
    {
        if (!showMarker) return;
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "PortalVisual";
        var col = marker.GetComponent<Collider>();
        if (col != null) Destroy(col);
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(0f, 2f, 0f);
        marker.transform.localScale    = new Vector3(0.75f, 2f, 0.75f); // ~1.5 wide, 4 tall
        var r = marker.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.2f, 0.9f, 1f);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
