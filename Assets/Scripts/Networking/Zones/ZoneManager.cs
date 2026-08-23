using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// M3.0 (Stage B) — server-side owner of the zone framework. Loads the <see cref="ZoneCatalog"/>,
/// additively loads every non-base zone scene, indexes each zone's <see cref="Scene"/> + entry points +
/// portals, polls portals for player proximity, and performs server-authoritative transitions.
///
/// Zones are authored AT their world offset (Z3-B); this manager never runtime-shifts content (the baked
/// NavMesh lives at authored coords). Lives on the NetworkManager GameObject; driven by
/// <see cref="GameNetworkManager"/> (ServerInitialize / ServerShutdown). Server-only — no client logic.
/// </summary>
public class ZoneManager : MonoBehaviour
{
    public static ZoneManager Instance { get; private set; }

    ZoneCatalog _catalog;
    readonly Dictionary<string, ZoneDefinition>                 _defs    = new();
    readonly Dictionary<string, Scene>                          _scenes  = new();
    readonly Dictionary<string, Dictionary<string, Transform>>  _entries = new();
    readonly List<ZonePortal>                                   _portals = new();
    readonly Dictionary<NetworkConnectionToClient, float>       _nextTransition = new();

    const float PortalPollInterval = 0.25f;
    const float TransitionDebounce = 1f;
    float _nextPoll;

    public string StarterZoneId =>
        _catalog != null && !string.IsNullOrEmpty(_catalog.starterZoneId)
            ? _catalog.starterZoneId : ZoneCatalog.DefaultStarterZoneId;

    // ── Lifecycle (called by GameNetworkManager) ─────────────────────────────────

    public void ServerInitialize()
    {
        Instance = this;
        _catalog = Resources.Load<ZoneCatalog>(ZoneCatalog.ResourcePath);
        if (_catalog == null)
        {
            Debug.LogError($"[Zone] No ZoneCatalog at Resources/{ZoneCatalog.ResourcePath}. " +
                           "Run Tools/Zones/Build Zone Scenes. Running single-scene (zones disabled).");
            return;
        }

        foreach (var z in _catalog.zones)
            if (z != null && !string.IsNullOrEmpty(z.zoneId))
                _defs[z.zoneId] = z;

        // The base scene is already loaded (the active gameplay scene). Map + index it now.
        foreach (var z in _catalog.zones)
            if (z != null && z.isBaseScene)
                RegisterScene(z.zoneId, SceneManager.GetActiveScene());

        // Additively load every non-base zone (or re-index it if already loaded, e.g. host restart in-editor).
        foreach (var z in _catalog.zones)
        {
            if (z == null || z.isBaseScene) continue;
            var existing = SceneManager.GetSceneByName(z.sceneName);
            if (existing.IsValid() && existing.isLoaded) RegisterScene(z.zoneId, existing);
            else                                          StartCoroutine(LoadZoneAsync(z));
        }
    }

    public void ServerShutdown()
    {
        StopAllCoroutines();
        if (Instance == this) Instance = null;
        _defs.Clear(); _scenes.Clear(); _entries.Clear(); _portals.Clear(); _nextTransition.Clear();
    }

    IEnumerator LoadZoneAsync(ZoneDefinition z)
    {
        var op = SceneManager.LoadSceneAsync(z.sceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError($"[Zone] Scene '{z.sceneName}' (zone '{z.zoneId}') not found / not in Build Settings.");
            yield break;
        }
        yield return op;
        RegisterScene(z.zoneId, SceneManager.GetSceneByName(z.sceneName));
        Debug.Log($"[Zone] Loaded zone '{z.zoneId}' ({z.sceneName}) at offset {z.worldOffset}.");
    }

    void RegisterScene(string zoneId, Scene scene)
    {
        if (!scene.IsValid()) return;
        _scenes[zoneId] = scene;
        if (!_entries.TryGetValue(zoneId, out var map)) { map = new Dictionary<string, Transform>(); _entries[zoneId] = map; }

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var e in root.GetComponentsInChildren<ZoneEntry>(true))
                map[e.entryId] = e.transform;
            foreach (var p in root.GetComponentsInChildren<ZonePortal>(true))
                if (!_portals.Contains(p)) _portals.Add(p);
        }

        MaterializePlacements(zoneId, scene);
    }

    // ── World placement sync (2.7.3, Stage A) ────────────────────────────────────
    // For every DB placement row belonging to this zone: refresh an already-scene-baked object's config
    // (WP5 — position/hierarchy stays whatever the scene authored; config always comes from the DB when a
    // row exists), or materialize one that isn't in the scene at all (ephemeral here — never written to a
    // scene asset, thrown away on server stop, exactly like SpawnPoint.SpawnOne's mob instantiation; the
    // Stage B Editor import tool calls the exact same PlacementMaterializer, just persists the result).
    void MaterializePlacements(string zoneId, Scene scene)
    {
        var byId = PlacementMaterializer.IndexScenePlacements(scene);
        PlacementMaterializer.SplitPasses(WorldPlacementRegistry.ForZone(zoneId), out var pass1, out var pass2);

        PlacementMaterializer.ApplyRows(pass1, byId, scene);
        PlacementMaterializer.ApplyRows(pass2, byId, scene);
        PlacementMaterializer.ResolveReferences(pass2, byId);
    }

    // ── Portal proximity poll (server) ───────────────────────────────────────────

    void Update()
    {
        if (!NetworkServer.active || _portals.Count == 0) return;
        if (Time.time < _nextPoll) return;
        _nextPoll = Time.time + PortalPollInterval;

        foreach (var conn in NetworkServer.connections.Values)
        {
            var id = conn?.identity;
            if (id == null) continue;
            if (id.GetComponent<NetworkedPlayer>() == null) continue;
            if (_nextTransition.TryGetValue(conn, out var next) && Time.time < next) continue;

            Vector3 pos = id.transform.position;
            for (int i = 0; i < _portals.Count; i++)
            {
                var p = _portals[i];
                if (p == null) continue;
                // Horizontal distance only — a portal at y=0 still triggers on uneven terrain where the
                // player walks at hill height.
                Vector3 d = pos - p.transform.position; d.y = 0f;
                if (d.sqrMagnitude <= p.radius * p.radius)
                {
                    ServerMovePlayer(conn, p.targetZoneId, p.targetEntryId);
                    _nextTransition[conn] = Time.time + TransitionDebounce;
                    break;
                }
            }
        }
    }

    // ── Transition ───────────────────────────────────────────────────────────────

    /// <summary>Move a connection's player to <paramref name="targetZoneId"/>, arriving at
    /// <paramref name="entryId"/>. Server-authoritative: scene move → client SceneMessage → ServerTeleport.</summary>
    public void ServerMovePlayer(NetworkConnectionToClient conn, string targetZoneId, string entryId)
    {
        var id = conn?.identity;
        if (id == null) return;
        if (!_scenes.TryGetValue(targetZoneId, out var targetScene) || !targetScene.IsValid())
        {
            Debug.LogWarning($"[Zone] Transition to unknown/unloaded zone '{targetZoneId}' ignored.");
            return;
        }

        var player   = id.GetComponent<NetworkedPlayer>();
        string fromZone = player != null ? player.CurrentZoneId : StarterZoneId;
        // ZA5: fromZone == targetZoneId is a valid intra-zone teleport (dungeon stairs, teleport pads) —
        // warp to the entry without a scene move or a client scene swap (the client already has the scene).

        // Resolve the arrival transform (world coords, at the zone's offset).
        Vector3 pos; float yaw;
        var entry = EntryTransform(targetZoneId, entryId);
        if (entry != null) { pos = entry.position; yaw = entry.eulerAngles.y; }
        else
        {
            var def = Def(targetZoneId);
            pos = def != null ? def.worldOffset : Vector3.zero;
            yaw = 0f;
            Debug.LogWarning($"[Zone] Entry '{entryId}' not found in '{targetZoneId}' — using zone offset.");
        }

        bool sameZone = fromZone == targetZoneId;

        // 1) Move the server-side player object into the destination scene (no-op for an intra-zone warp;
        //    interest rebuild isolates it for a cross-zone move).
        if (!id.gameObject.scene.Equals(targetScene))
            SceneManager.MoveGameObjectToScene(id.gameObject, targetScene);

        // 2) Cross-zone only: tell the client to additively load the destination + unload the previous
        //    additive zone. The base scene is the client's main scene and is never unloaded.
        if (!sameZone)
            SendClientSceneSwap(conn, fromZone, targetZoneId);

        // 3) Server-authoritative teleport to the entry + record the (possibly unchanged) zone.
        player?.SetZone(targetZoneId);
        player?.ServerWarpTo(pos, yaw);

        // 4) Keep the chat spatial grid in sync with the jump.
        ChatManager.Instance?.UpdatePosition(conn, pos);

        Debug.Log(sameZone
            ? $"[Zone] conn {conn.connectionId}: intra-zone warp in '{targetZoneId}' @ '{entryId}' ({pos})."
            : $"[Zone] conn {conn.connectionId}: {fromZone} → {targetZoneId} @ '{entryId}' ({pos}).");
    }

    /// <summary>Login placement (Stage C): put a just-spawned player into their PERSISTED zone at their
    /// SAVED position (not a portal entry). Mirror spawns the player in the base scene; if the saved zone is
    /// a different scene we move the object there + tell the client to load it additively, then warp to the
    /// saved world position. Unknown/unloaded zone (e.g. removed from the catalog) falls back to the starter
    /// zone so the player never spawns into a dead scene.</summary>
    [Server]
    public void ServerPlaceInZone(NetworkConnectionToClient conn, string zoneId, Vector3 position, float yaw)
    {
        var id = conn?.identity;
        if (id == null) return;

        if (string.IsNullOrEmpty(zoneId) || !_scenes.TryGetValue(zoneId, out var targetScene) || !targetScene.IsValid())
        {
            if (!string.IsNullOrEmpty(zoneId))
                Debug.LogWarning($"[Zone] Login zone '{zoneId}' unknown/unloaded — placing in {StarterZoneId}.");
            zoneId = StarterZoneId;
            _scenes.TryGetValue(zoneId, out targetScene);
            // The saved position is in the (now gone) zone's coords — don't strand the player out there.
            // Arrive at the starter's default entry instead.
            var e = EntryTransform(zoneId, "default");
            if (e != null) { position = e.position; yaw = e.eulerAngles.y; }
        }

        string fromZone = ZoneIdForScene(id.gameObject.scene);

        if (targetScene.IsValid() && !id.gameObject.scene.Equals(targetScene))
            SceneManager.MoveGameObjectToScene(id.gameObject, targetScene);

        SendClientSceneSwap(conn, fromZone, zoneId);

        var player = id.GetComponent<NetworkedPlayer>();
        player?.SetZone(zoneId);
        player?.ServerWarpTo(position, yaw);
        ChatManager.Instance?.UpdatePosition(conn, position);

        if (fromZone != zoneId)
            Debug.Log($"[Zone] conn {conn.connectionId}: login placed in '{zoneId}' @ saved pos {position}.");
    }

    void SendClientSceneSwap(NetworkConnectionToClient conn, string fromZone, string toZone)
    {
        var toDef = Def(toZone);
        if (toDef != null && !toDef.isBaseScene)
            conn.Send(new SceneMessage { sceneName = toDef.sceneName, sceneOperation = SceneOperation.LoadAdditive });

        var fromDef = Def(fromZone);
        if (fromDef != null && !fromDef.isBaseScene && fromZone != toZone)
            conn.Send(new SceneMessage { sceneName = fromDef.sceneName, sceneOperation = SceneOperation.UnloadAdditive });
    }

    // ── Lookups ──────────────────────────────────────────────────────────────────

    public ZoneDefinition Def(string zoneId) => _defs.TryGetValue(zoneId, out var d) ? d : null;

    public bool TryGetScene(string zoneId, out Scene scene)
        => _scenes.TryGetValue(zoneId, out scene) && scene.IsValid();

    /// <summary>The world transform of a named entry (falls back to the zone's "default" entry).</summary>
    public Transform EntryTransform(string zoneId, string entryId)
    {
        if (_entries.TryGetValue(zoneId, out var map))
        {
            if (!string.IsNullOrEmpty(entryId) && map.TryGetValue(entryId, out var t) && t != null) return t;
            if (map.TryGetValue("default", out var d) && d != null) return d;
        }
        return null;
    }

    public string ZoneIdForScene(Scene scene)
    {
        foreach (var kv in _scenes)
            if (kv.Value == scene) return kv.Key;
        return StarterZoneId;
    }
}
