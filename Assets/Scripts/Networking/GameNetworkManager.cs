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
            GetComponent<ZoneManager>()?.ServerInitialize(); // 3.0 — additive zones + interest partitioning
            GetComponent<PartyManager>()?.ServerInitialize(); // 5.3 — session-only party registry
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
        GetComponent<PartyManager>()?.ServerShutdown(); // 5.3 — session-only, nothing to persist
        GetComponent<ZoneManager>()?.ServerShutdown(); // 3.0 — clear zone state (scenes torn down by Unity)

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
        // 5.3 (GP1/GP3) — session-only groups: a disconnect is treated exactly like /leave.
        PartyManager.Instance?.HandleDisconnect(conn?.identity);
        base.OnServerDisconnect(conn);
        Debug.Log($"[Server] Client disconnected: {conn.connectionId}");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[Client] Connected to server.");
    }

    // Register/unregister the content catalog handler on the client (2.2 — DB-backed content reaches
    // clients over Mirror, never via the DB directly).
    public override void OnStartClient()
    {
        base.OnStartClient();
        NetworkClient.RegisterHandler<ContentCatalog.ItemCatalogMessage>(ContentCatalog.ApplyItems);
        // 2.9 — abilities need client sync too (HotbarUI reads AbilityRegistry to label slots).
        NetworkClient.RegisterHandler<ContentCatalog.AbilityCatalogMessage>(ContentCatalog.ApplyAbilities);
        // 2.10 — races/classes need client sync too (CharacterModelFactory/CharacterPreview read RaceClassRegistry).
        NetworkClient.RegisterHandler<ContentCatalog.RaceCatalogMessage>(ContentCatalog.ApplyRaces);
        NetworkClient.RegisterHandler<ContentCatalog.ClassCatalogMessage>(ContentCatalog.ApplyClasses);
        // 3.0 — chat is delivered via a NetworkMessage (not an RPC) so it survives zone/scene interest
        // partitioning. Register the client-side receiver.
        NetworkClient.RegisterHandler<ChatDeliverMessage>(ChatManager.HandleDeliver);
    }

    public override void OnStopClient()
    {
        NetworkClient.UnregisterHandler<ContentCatalog.ItemCatalogMessage>();
        NetworkClient.UnregisterHandler<ContentCatalog.AbilityCatalogMessage>();
        NetworkClient.UnregisterHandler<ContentCatalog.RaceCatalogMessage>();
        NetworkClient.UnregisterHandler<ContentCatalog.ClassCatalogMessage>();
        NetworkClient.UnregisterHandler<ChatDeliverMessage>();
        base.OnStopClient();
    }

    // Push the content catalog to a client as it becomes ready, before its player spawns so inventory/
    // tooltips have item data to read. Host receives it too but ContentCatalog.ApplyItems no-ops there.
    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        conn.Send(ContentCatalog.BuildItems());
        conn.Send(ContentCatalog.BuildAbilities());
        conn.Send(ContentCatalog.BuildRaces());
        conn.Send(ContentCatalog.BuildClasses());
        base.OnServerReady(conn);
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
