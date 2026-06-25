using Mirror;
using UnityEngine;

// Central server authority. Extend here for enemy spawning, match state, etc.
public class GameNetworkManager : NetworkManager
{
    // Spawn points are registered via NetworkStartPosition components in the scene.
    // The base NetworkManager.OnServerAddPlayer already handles player spawning
    // at registered start positions — no override needed until we add custom logic.

    // Connect + migrate + seed the database before the world goes live. If the DB is
    // unavailable we abort with a loud error rather than run without persistence.
    public override void OnStartServer()
    {
        base.OnStartServer();
        try
        {
            Database.InitializeServer();
            ContentLoader.LoadAll(); // 2.1 — load DB-backed content into registries before the world goes live
            PersistenceService.Create();
            GetComponent<CharacterSelectController>()?.OnServerStarted(); // 1.5 select/create handlers
            InvokeRepeating(nameof(AutosaveTick), AutosaveSeconds, AutosaveSeconds); // 1.6 autosave (O3)
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                "[Server] ABORTING server start — database unavailable. " +
                "Start Postgres (docker compose up -d) and try again.\n" + e);
            StopServer();
        }
    }

    // Periodic autosave (1.6, O3). Cheap: per-character saves coalesce in the 1.2 write queue.
    const float AutosaveSeconds = 90f;
    void AutosaveTick() => SaveAllCharacters();

    // Flush queued DB writes and stop the persistence worker before the server tears down.
    public override void OnStopServer()
    {
        CancelInvoke(nameof(AutosaveTick));
        GetComponent<CharacterSelectController>()?.OnServerStopped(); // 1.5 — unregister select handlers

        // Save every connected character FIRST — Mirror destroys player objects during the
        // subsequent NetworkServer.Shutdown, and we must enqueue before FlushAndStop drains.
        SaveAllCharacters();

        base.OnStopServer();
        if (PersistenceService.Instance != null)
        {
            PersistenceService.Instance.FlushAndStop();
            Destroy(PersistenceService.Instance.gameObject);
        }
    }

    // Save-on-quit (1.6, O3): capture live characters before the persistence worker flushes. Order is
    // undefined across OnApplicationQuit handlers, so we drive save-then-flush here explicitly
    // (FlushAndStop is idempotent).
    public override void OnApplicationQuit()
    {
        if (NetworkServer.active)
        {
            SaveAllCharacters();
            PersistenceService.Instance?.FlushAndStop();
        }
        base.OnApplicationQuit();
    }

    // Enqueue a save for every connected player's character (1.3).
    void SaveAllCharacters()
    {
        foreach (var conn in NetworkServer.connections.Values)
            conn?.identity?.GetComponent<CharacterPersistence>()?.Save();
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Debug.Log($"[Server] Client connected: {conn.connectionId}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        // Persist this player's character before base tears the player object down (1.3).
        conn?.identity?.GetComponent<CharacterPersistence>()?.Save();
        // Free single-login state before Mirror tears the connection down.
        (authenticator as AccountAuthenticator)?.HandleServerDisconnect(conn);
        base.OnServerDisconnect(conn);
        Debug.Log($"[Server] Client disconnected: {conn.connectionId}");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[Client] Connected to server.");
    }

    // Called on server when a new player is added. The authenticated account rides on the
    // connection (set by AccountAuthenticator) — this is the identity seam 1.3/1.5 consume to
    // resolve the character. For 1.4 we just confirm it's present.
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        if (conn.authenticationData is AccountSession session)
            Debug.Log($"[Server] Player added for account #{session.AccountId} ({session.Username}).");
        else
            Debug.LogWarning("[Server] Player added with no account session — auth seam missing.");
    }

    // ── Future hooks ─────────────────────────────────────────────────────────
    // SpawnEnemy(Vector3 position) — instantiate + NetworkServer.Spawn
    // DespawnEnemy(GameObject enemy) — NetworkServer.Destroy
    // OnPlayerKilledEnemy(NetworkIdentity killer, NetworkIdentity victim)
}
