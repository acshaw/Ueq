using UnityEngine;

/// <summary>
/// Server-only entry point that loads DB-backed content into the in-memory registries
/// at host start. This is the reusable seam (2.1): each content type adds a step here that
/// reads its rows (via a repository) and pushes them into its registry — swapping the
/// *source* behind each registry's existing lookup-by-id API from <c>Resources.LoadAll</c>
/// to Postgres. 2.1 ships the pattern with one throwaway type (content_ping); 2.2 (items)
/// is the first real registry to adopt it.
///
/// Runs synchronously on the main thread right after <see cref="Database.InitializeServer"/>,
/// before the world goes live — same model as migrations. Content sets are small and read
/// once; this is not on the gameplay tick. Throwing here aborts server start (the caller in
/// <c>GameNetworkManager.OnStartServer</c> wraps this), so a content/DB problem fails loudly
/// instead of booting a world with empty registries.
/// </summary>
public static class ContentLoader
{
    /// <summary>Load every DB-backed content type into its registry. Add a step per type as it migrates.</summary>
    public static void LoadAll()
    {
        using var conn = Database.OpenConnection();

        // ── content_ping (2.1 smoke — proves the Angular→API→DB→Unity chain) ─────────────
        var pings = new ContentPingRepository().LoadAll(conn);
        Debug.Log($"[Content] Loaded {pings.Count} content_ping row(s) from the database.");
        foreach (var p in pings)
            Debug.Log($"[Content]   ping #{p.Id}: {p.Label}");

        // ── 2.2+: items, abilities, etc. register their loads below as each type migrates ──
    }
}
