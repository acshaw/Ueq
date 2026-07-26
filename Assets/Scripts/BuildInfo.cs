using UnityEngine;

/// <summary>
/// 6.4 — a single stamped identifier shared by whichever client/server builds were produced
/// together for one release. Read at runtime via <see cref="Current"/> by both
/// <c>AccountAuthenticator</c> (server-side mismatch check, client-side value to send) and the
/// standalone launcher's <c>version.txt</c> artifact (stamped alongside this asset, not read from
/// it directly — the launcher never runs Unity code).
///
/// Deliberately NOT re-stamped by every individual build method in <c>ServerBuildTools</c> — only
/// <c>Tools/Build/Stamp New Build Id</c> changes it, so building the client and the server
/// separately (in either order) for the same release still embeds the same id. Re-stamp before
/// cutting a new release, then build whichever of client/server changed.
/// </summary>
public class BuildInfo : ScriptableObject
{
    public string buildId = "";

    const string ResourcePath = "BuildInfo";

    static BuildInfo _cached;

    /// <summary>Empty string if no BuildInfo asset exists yet (e.g. a fresh Editor Play session
    /// that's never run the stamp tool) — callers should treat that as "don't enforce the check."</summary>
    public static string Current
    {
        get
        {
            if (_cached == null)
                _cached = Resources.Load<BuildInfo>(ResourcePath);
            return _cached != null ? _cached.buildId : "";
        }
    }
}
