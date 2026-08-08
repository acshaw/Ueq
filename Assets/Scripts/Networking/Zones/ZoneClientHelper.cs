using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 5.12 (DC7) — client-side read of whether the local player's current zone wants the day/night sky
/// visuals (sun/sky/moon/clouds) enabled, e.g. a future underground dungeon opting out for static
/// lighting. Loads the client's own copy of <see cref="ZoneCatalog"/> — a Resources asset shipped with
/// the build, not DB content, the same asset the server loads — and matches it against the local
/// player's <c>GameObject.scene</c>.
///
/// NOTE (flagged in the 5.12 devplan's Risks section): this assumes a client's own player object's Unity
/// scene reflects the zone it's actually standing in after a zone transition. That assumption is not yet
/// verified against a real second zone type — Thornwood exists and can serve as a synthetic test (flip
/// its <see cref="ZoneDefinition.usesDayNightCycle"/> off and confirm the sky visuals actually hide
/// there). Fails open (cycle enabled) if anything can't be resolved — "sky always shows" is the harmless
/// default versus a broken/inverted gate.
/// </summary>
public static class ZoneClientHelper
{
    static ZoneCatalog _catalog;

    public static bool CurrentZoneUsesDayNightCycle()
    {
        var player = LocalPlayer.Current;
        if (player == null) return true;

        if (_catalog == null) _catalog = Resources.Load<ZoneCatalog>(ZoneCatalog.ResourcePath);
        if (_catalog == null) return true;

        Scene scene = player.gameObject.scene;
        Scene active = SceneManager.GetActiveScene();

        foreach (var z in _catalog.zones)
        {
            if (z == null) continue;
            bool matches = z.isBaseScene ? scene == active : z.sceneName == scene.name;
            if (matches) return z.usesDayNightCycle;
        }
        return true;
    }
}
