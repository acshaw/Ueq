using Mirror;
using UnityEngine;

// Central server authority. Extend here for enemy spawning, match state, etc.
public class GameNetworkManager : NetworkManager
{
    // Spawn points are registered via NetworkStartPosition components in the scene.
    // The base NetworkManager.OnServerAddPlayer already handles player spawning
    // at registered start positions — no override needed until we add custom logic.

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Debug.Log($"[Server] Client connected: {conn.connectionId}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        Debug.Log($"[Server] Client disconnected: {conn.connectionId}");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[Client] Connected to server.");
    }

    // Called on server when a new player is added — good hook for future
    // "send initial game state to new joiner" logic.
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
    }

    // ── Future hooks ─────────────────────────────────────────────────────────
    // SpawnEnemy(Vector3 position) — instantiate + NetworkServer.Spawn
    // DespawnEnemy(GameObject enemy) — NetworkServer.Destroy
    // OnPlayerKilledEnemy(NetworkIdentity killer, NetworkIdentity victim)
}
