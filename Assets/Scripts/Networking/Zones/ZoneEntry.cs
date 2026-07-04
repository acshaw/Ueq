using UnityEngine;

/// <summary>
/// M3.0 (Z4) — a named arrival point inside a zone scene. A <see cref="ZonePortal"/> targets one by
/// (zoneId, entryId), and character-select / respawn resolve the persisted zone's default entry.
/// World position + yaw come from the transform (authored at the zone's world offset). Also used as the
/// zone's default player spawn when <c>entryId == "default"</c>.
/// </summary>
public class ZoneEntry : MonoBehaviour
{
    [Tooltip("Identifier a ZonePortal.targetEntryId matches against. Use \"default\" for the zone's " +
             "primary spawn/arrival point.")]
    public string entryId = "default";

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, 1f);
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
}
