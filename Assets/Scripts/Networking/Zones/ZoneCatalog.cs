using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// M3.0 (Z1) — one zone's boot record: its id, the scene that holds its geometry/navmesh/spawns, and the
/// world-space offset that content is authored at. Pure data; no behaviour.
/// </summary>
[Serializable]
public class ZoneDefinition
{
    public string zoneId = "";

    [Tooltip("Scene file name (no path/extension) that holds this zone's geometry, navmesh, spawns.")]
    public string sceneName = "";

    [Tooltip("World-space offset this zone's content is authored at (Z3-B). Zones must not overlap, and " +
             "spacing must exceed any query radius (say/activation). The baked NavMesh lives at these " +
             "coords too, so content is authored here — ZoneManager does NOT runtime-shift.")]
    public Vector3 worldOffset = Vector3.zero;

    [Tooltip("True for the base scene that is already loaded when the server starts (the starter zone " +
             "under decision A). ZoneManager additively loads only the non-base zones.")]
    public bool isBaseScene = false;
}

/// <summary>
/// M3.0 (Z1) — serialized zone catalog (the boot list). Lives in <c>Resources/</c> and is loaded by
/// <c>ZoneManager</c> at server start. Deliberately not DB-backed: a zone's scene is a Unity build, so it
/// can't be web-authored end-to-end (see the 3.0 devplan Z1). DB-backed zones are a future item.
/// </summary>
[CreateAssetMenu(fileName = "ZoneCatalog", menuName = "Ueq/Zone Catalog")]
public class ZoneCatalog : ScriptableObject
{
    /// <summary>Compile-time default used before the catalog is loaded (and as the migration default).</summary>
    public const string DefaultStarterZoneId = "creslins_field";

    /// <summary>Resources path (no extension) the ZoneManager loads the catalog from.</summary>
    public const string ResourcePath = "ZoneCatalog";

    public string starterZoneId = DefaultStarterZoneId;
    public List<ZoneDefinition> zones = new();

    public ZoneDefinition Get(string zoneId)
    {
        for (int i = 0; i < zones.Count; i++)
            if (zones[i] != null && zones[i].zoneId == zoneId) return zones[i];
        return null;
    }
}
