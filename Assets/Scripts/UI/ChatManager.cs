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
                TargetDeliver(conn, msg.Channel, msg.SenderName, msg.Text);
        }
    }

    /// Deliver to one specific connection (whispers, personal rewards, system errors).
    [Server]
    public void SendDirect(ChatMessage msg, NetworkConnectionToClient conn)
        => TargetDeliver(conn, msg.Channel, msg.SenderName, msg.Text);

    /// Deliver to every connected client (server announcements).
    [Server]
    public void SendAll(ChatMessage msg)
        => RpcDeliver(msg.Channel, msg.SenderName, msg.Text);

    [Server]
    public NetworkConnectionToClient FindConnectionByName(string playerName)
    {
        foreach (var conn in NetworkServer.connections.Values)
            if (conn.identity != null && conn.identity.name == playerName) return conn;
        return null;
    }

    // ── RPCs ──────────────────────────────────────────────────────────────────

    [TargetRpc]
    void TargetDeliver(NetworkConnectionToClient conn, ChatChannel channel, string sender, string text)
        => ChatUI.Receive(new ChatMessage(channel, sender, text));

    [ClientRpc]
    void RpcDeliver(ChatChannel channel, string sender, string text)
        => ChatUI.Receive(new ChatMessage(channel, sender, text));

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
