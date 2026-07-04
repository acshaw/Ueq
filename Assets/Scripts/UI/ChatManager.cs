using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class ChatManager : NetworkBehaviour
{
    public static ChatManager Instance { get; private set; }

    [SerializeField] float sayRadius   = 30f;
    [SerializeField] float shoutRadius = 120f;
    [SerializeField] float cellSize    = 130f; // must be >= shoutRadius

    // Server-side spatial grid
    readonly Dictionary<Vector2Int, HashSet<NetworkConnectionToClient>> _grid     = new();
    readonly Dictionary<NetworkConnectionToClient, Vector3>             _connPos  = new();
    readonly Dictionary<NetworkConnectionToClient, Vector2Int>          _connCell = new();

    void Awake()  => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    // ── Player registration ───────────────────────────────────────────────────

    [Server]
    public void RegisterPlayer(NetworkConnectionToClient conn, Vector3 pos)
    {
        _connPos[conn] = pos;
        MoveToCell(conn, CellFor(pos));
    }

    [Server]
    public void UnregisterPlayer(NetworkConnectionToClient conn)
    {
        if (conn == null) return;
        if (_connCell.TryGetValue(conn, out var cell) && _grid.TryGetValue(cell, out var set))
            set.Remove(conn);
        _connCell.Remove(conn);
        _connPos.Remove(conn);
    }

    [Server]
    public void UpdatePosition(NetworkConnectionToClient conn, Vector3 pos)
    {
        _connPos[conn] = pos;
        var newCell = CellFor(pos);
        if (_connCell.TryGetValue(conn, out var oldCell) && oldCell == newCell) return;
        MoveToCell(conn, newCell);
    }

    // ── Send API ──────────────────────────────────────────────────────────────

    /// Deliver to all players within radius of origin. Radius is determined by channel
    /// (Shout uses shoutRadius; everything else uses sayRadius).
    [Server]
    public void SendArea(ChatMessage msg, Vector3 origin)
    {
        float radius = msg.Channel == ChatChannel.Shout ? shoutRadius : sayRadius;
        foreach (var conn in GetCandidates(origin, radius))
        {
            if (!_connPos.TryGetValue(conn, out var p)) continue;
            if (Vector3.Distance(origin, p) <= radius)
                Deliver(conn, msg);
        }
    }

    /// Deliver to one specific connection (whispers, personal rewards, system errors).
    [Server]
    public void SendDirect(ChatMessage msg, NetworkConnectionToClient conn)
        => Deliver(conn, msg);

    /// Deliver to every connected client (server announcements).
    [Server]
    public void SendAll(ChatMessage msg)
    {
        foreach (var conn in NetworkServer.connections.Values)
            Deliver(conn, msg);
    }

    [Server]
    public NetworkConnectionToClient FindConnectionByName(string playerName)
    {
        foreach (var conn in NetworkServer.connections.Values)
            if (conn.identity != null && conn.identity.name == playerName) return conn;
        return null;
    }

    // ── Delivery (3.0) ──────────────────────────────────────────────────────────
    // Delivery goes over conn.Send (a NetworkMessage), NOT a TargetRpc/ClientRpc. Under
    // SceneInterestManagement a player in another zone is not an observer of this base-scene singleton,
    // so an RPC issued from it would silently drop for them. conn.Send is independent of object
    // observation (the same pattern ContentCatalog uses). The client handler is registered by
    // GameNetworkManager (OnStartClient) → HandleDeliver.

    [Server]
    static void Deliver(NetworkConnectionToClient conn, ChatMessage msg)
    {
        if (conn == null) return;
        conn.Send(new ChatDeliverMessage { channel = msg.Channel, sender = msg.SenderName, text = msg.Text });
    }

    /// Client-side handler for a delivered chat line (registered by GameNetworkManager).
    public static void HandleDeliver(ChatDeliverMessage msg)
        => ChatUI.Receive(new ChatMessage(msg.channel, msg.sender, msg.text));

    // ── Spatial grid ──────────────────────────────────────────────────────────

    Vector2Int CellFor(Vector3 pos)
        => new Vector2Int(Mathf.FloorToInt(pos.x / cellSize), Mathf.FloorToInt(pos.z / cellSize));

    void MoveToCell(NetworkConnectionToClient conn, Vector2Int newCell)
    {
        if (_connCell.TryGetValue(conn, out var oldCell) && _grid.TryGetValue(oldCell, out var oldSet))
            oldSet.Remove(conn);

        _connCell[conn] = newCell;
        if (!_grid.ContainsKey(newCell)) _grid[newCell] = new HashSet<NetworkConnectionToClient>();
        _grid[newCell].Add(conn);
    }

    // Broadphase: return all connections in cells that could overlap the radius.
    // Narrowphase (exact distance check) is done by the caller.
    List<NetworkConnectionToClient> GetCandidates(Vector3 center, float radius)
    {
        var results = new List<NetworkConnectionToClient>();
        var origin  = CellFor(center);
        int span    = Mathf.CeilToInt(radius / cellSize) + 1;

        for (int dx = -span; dx <= span; dx++)
        for (int dz = -span; dz <= span; dz++)
        {
            if (_grid.TryGetValue(new Vector2Int(origin.x + dx, origin.y + dz), out var set))
                results.AddRange(set);
        }
        return results;
    }
}

/// <summary>3.0 — chat line delivered server→client via <c>conn.Send</c> (observation-independent, so it
/// survives SceneInterestManagement partitioning). Handled client-side by <c>ChatManager.HandleDeliver</c>.</summary>
public struct ChatDeliverMessage : NetworkMessage
{
    public ChatChannel channel;
    public string      sender;
    public string      text;
}
