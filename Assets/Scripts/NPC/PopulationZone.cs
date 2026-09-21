using System.Collections;
using System.Collections.Generic;
using Mirror;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 8.6 — an area-based population spawner: maintains up to <see cref="maxPopulation"/> mobs inside an
/// authored <see cref="WanderRegion"/> (normally Polygon-shaped, though any shape works), replenished by
/// independent per-death respawn timers rather than a fixed set of point camps. A different mechanism in
/// kind from <see cref="SpawnPoint"/> (point-based, one location, one respawn cycle per group) — this is
/// one shared ceiling across a whole region with many independent replacement timers in flight at once, so
/// sustained hunting pressure can genuinely drain the population toward 0.
///
/// Always-simulating (PZ4): populates once at zone load and every replacement timer fires unconditionally,
/// regardless of player presence. This project already runs every spawned mob's full simulation
/// (NavMeshAgent wander, perception ticks) regardless of proximity — there is no distance-based unload
/// system anywhere — so this adds no new cost category, only whether a *replacement* is allowed to appear
/// while nobody's watching, which is exactly as cheap as any other spawn.
/// </summary>
public class PopulationZone : MonoBehaviour, IWorldPlacement, IReferencesOtherPlacements
{
    [Header("World Placement Sync (2.7.3)")]
    [Tooltip("GUID assigned once when this object is placed. Never hand-edit.")]
    [SerializeField] string placementId = "";

    [Tooltip("8.6: spawn from a DB-backed spawn table (weighted entries; calendar conditions and per-entry " +
             "respawn/level overrides all apply, same as SpawnPoint — SpawnTableEntry.groupSize is ignored " +
             "here, PZ7, since a population zone fills one slot per roll). Highest precedence.")]
    [SerializeField] string spawnTableId = "";
    [Tooltip("8.6: spawn a single DB-backed mob by its id. Used when no spawnTableId is set.")]
    [SerializeField] string mobId = "";

    [Tooltip("8.6: the maximum number of live members this zone maintains at once.")]
    [SerializeField] int maxPopulation = 10;
    [Tooltip("8.6: base respawn delay (seconds) after a death, unless that specific roll's entry has its " +
             "own 8.2 respawn override.")]
    [SerializeField] float respawnBase = 210f;
    [Tooltip("8.6: ± variance on the base respawn delay.")]
    [SerializeField] float respawnVariance = 90f;

    [Tooltip("8.6: the authored area (normally Polygon-shaped) mobs spawn and wander within. Required — " +
             "nothing spawns without one.")]
    [SerializeField] WanderRegion region;

    // Read-only accessor for editor scene-view labels, mirrors SpawnPoint's own accessors.
    public bool HasRegion => region != null;

    bool _populated;
    readonly List<NetworkIdentity> _live = new();
    // 8.2/8.3 threading (PZ8) — SpawnPoint gets this for free via one shared _lastRolledEntry since its
    // whole group comes from one roll; a population zone has many independently-rolled members alive at
    // once, so each needs its own remembered entry for when *that specific one* dies.
    readonly Dictionary<NetworkIdentity, SpawnTableEntry> _liveEntries = new();

    // ── World Placement Sync (2.7.3, Stage A) ──────────────────────────────────

    public string PlacementId => placementId;
    public string MarkerType  => "PopulationZone";
    public void   SetPlacementId(string id) => placementId = id;

    string _pendingRegionPlacementId;

    public JObject CapturePlacementData() => new()
    {
        ["spawnTableId"]      = spawnTableId,
        ["mobId"]             = mobId,
        ["maxPopulation"]     = maxPopulation,
        ["respawnBase"]       = respawnBase,
        ["respawnVariance"]   = respawnVariance,
        ["regionPlacementId"] = region != null ? region.PlacementId : null,
    };

    // Config only — never touches position/rotation (WP5: the scene/row's position columns own that).
    public void ApplyPlacementData(JObject data)
    {
        spawnTableId    = (string)data["spawnTableId"] ?? "";
        mobId           = (string)data["mobId"] ?? "";
        maxPopulation   = (int?)data["maxPopulation"] ?? maxPopulation;
        respawnBase     = (float?)data["respawnBase"] ?? respawnBase;
        respawnVariance = (float?)data["respawnVariance"] ?? respawnVariance;

        _pendingRegionPlacementId = (string)data["regionPlacementId"];
    }

    // Two-pass resolution (WP3), same shape SpawnPoint already uses for patrolRoute/wanderRegion/pool.
    public void ResolveReferences(IReadOnlyDictionary<string, GameObject> byPlacementId)
    {
        if (!string.IsNullOrEmpty(_pendingRegionPlacementId))
        {
            if (byPlacementId.TryGetValue(_pendingRegionPlacementId, out var go))
                region = go.GetComponent<WanderRegion>();
            else
                Debug.LogWarning($"[Placement] {name}: wander region '{_pendingRegionPlacementId}' not " +
                                 "found among this zone's placements — population zone has no area, nothing will spawn.", this);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(placementId))
            placementId = System.Guid.NewGuid().ToString();
    }
#endif

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (!NetworkServer.active) return;

        InitialFill();
        // 8.6 — periodic self-healing prune (NOT an activation/proximity gate — PZ4 dropped that entirely).
        // Catches a mob destroyed via a path that skips Health.OnDied, which would otherwise leave a stale
        // _live/_liveEntries entry counting toward maxPopulation forever with no timer ever scheduled to
        // replace it. Same defensive spirit as SpawnPoint.ActivationCheck's own null-prune, just on a
        // longer interval since this is a safety net, not time-critical.
        InvokeRepeating(nameof(SelfHealPrune), 30f, 30f);
    }

    void InitialFill()
    {
        if (_populated) return;
        _populated = true;

        if (region == null)
        {
            Debug.LogWarning($"[PopulationZone] {name}: no region set — nothing will spawn.", this);
            return;
        }

        for (int i = 0; i < maxPopulation; i++)
            SpawnOne();
    }

    void SelfHealPrune()
    {
        int before = _live.Count;
        _live.RemoveAll(id => id == null);

        if (_liveEntries.Count > 0)
        {
            List<NetworkIdentity> stale = null;
            foreach (var key in _liveEntries.Keys)
            {
                if (key != null) continue;
                (stale ??= new List<NetworkIdentity>()).Add(key);
            }
            if (stale != null)
                foreach (var key in stale) _liveEntries.Remove(key);
        }

        // A mob lost without dying properly (skips OnMemberDied/RespawnAfterDelay entirely) would
        // otherwise never get replaced — top up immediately rather than waiting on a timer nothing ever
        // started. Only one per tick; a future tick (or the mob's own eventual real death) catches the rest.
        if (before > _live.Count && _live.Count < maxPopulation)
            SpawnOne();
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    void SpawnOne()
    {
        if (region == null) return;

        MobDefinition   def;
        SpawnTableEntry entry = null;

        var table = SpawnTableRegistry.Get(spawnTableId);
        if (table != null)
        {
            // Reuses SpawnTable.Roll() verbatim — a free side effect (not extra work here) is that 8.1.4's
            // calendar conditions (day/night, lunar phase, day-of-week, month) already apply with zero
            // population-zone-specific code, same TG3 quiet-vs-warn distinction SpawnPoint.DoSpawn uses.
            entry = table.Roll(out bool anyEntriesExist);
            if (entry == null)
            {
                if (!anyEntriesExist)
                    Debug.LogWarning($"[PopulationZone] {name}: spawn table '{spawnTableId}' has no valid " +
                                     "entries (empty, or every mob reference is unresolved).", this);
                return;
            }
            def = entry.mob;
        }
        else if (!string.IsNullOrEmpty(mobId))
        {
            def = MobRegistry.Get(mobId);
        }
        else
        {
            Debug.LogWarning($"[PopulationZone] {name}: nothing configured to spawn " +
                             "(set spawnTableId or mobId).", this);
            return;
        }

        if (def?.prefab == null)
        {
            Debug.LogWarning($"[PopulationZone] {name}: no valid mob to spawn " +
                             $"(spawnTableId='{spawnTableId}', mobId='{mobId}', prefab missing/unregistered).", this);
            return;
        }

        // Reuses BoundedRegion.TryGetRandomPoint verbatim (PZ1 grounding) — the exact sample+navmesh-snap
        // pipeline WanderBehavior already uses for ongoing wander, not just initial placement.
        if (!new BoundedRegion(region).TryGetRandomPoint(out var point))
        {
            Debug.LogWarning($"[PopulationZone] {name}: couldn't find a valid navmesh point inside the " +
                             "region for this spawn — skipping (will retry on the next death or prune tick).", this);
            return;
        }

        var go = Instantiate(def.prefab, point, Quaternion.identity);

        // Same zone-scene assignment SpawnPoint.SpawnOne already does — a population zone is just as much
        // a placed-in-a-zone-scene object.
        if (gameObject.scene.IsValid() && go.scene != gameObject.scene)
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);

        var mobApp = go.GetComponent<MobApplicator>();
        mobApp?.SetDefinition(def);

        // 8.3 (PZ8) — per-instance level roll, only if this entry configured a range. Identical to
        // SpawnPoint.SpawnOne's own block, just called from this different spawn site.
        if (entry?.minLevelOverride is int minLvl && entry?.maxLevelOverride is int maxLvl)
            mobApp?.SetLevelOverride(Random.Range(minLvl, maxLvl + 1));

        // Every population-zone mob wanders bounded to this zone's region — never a spawn leash, since
        // there's no meaningful single "spawn point" for an area-based spawner (PZ2).
        var wander = go.GetComponent<WanderBehavior>();
        wander?.SetBoundedRegion(region);

        NetworkServer.Spawn(go);

        var id = go.GetComponent<NetworkIdentity>();
        _live.Add(id);
        if (entry != null) _liveEntries[id] = entry;

        var health = go.GetComponent<Health>();
        if (health != null)
        {
            System.Action<NetworkIdentity> handler = null;
            handler = _ => { health.OnDied -= handler; OnMemberDied(id); };
            health.OnDied += handler;
        }
    }

    void OnMemberDied(NetworkIdentity id)
    {
        _live.Remove(id);
        _liveEntries.TryGetValue(id, out var diedEntry);
        _liveEntries.Remove(id);

        // 8.6 (PZ4): always simulating — no activation/proximity check before scheduling the replacement.
        StartCoroutine(RespawnAfterDelay(diedEntry));
    }

    IEnumerator RespawnAfterDelay(SpawnTableEntry diedEntry)
    {
        float delay = ResolveRespawnDelay(diedEntry);
        yield return new WaitForSeconds(delay);

        // 8.6 (PZ3): only replace if still under the cap — _live.Count is always accurate (deaths remove
        // immediately in OnMemberDied, not deferred to when this fires), so independent concurrent death
        // timers never double-count or overshoot maxPopulation.
        if (_live.Count < maxPopulation)
            SpawnOne();
    }

    // 8.2 (PZ8): the died entry's own respawn override, if set, takes precedence over the zone's default —
    // same base±variance formula SpawnPoint.ResolveRespawnDelay/SpawnTimer.Roll() already use.
    float ResolveRespawnDelay(SpawnTableEntry diedEntry)
    {
        if (diedEntry?.respawnBaseSeconds is float baseSeconds)
        {
            float variance = diedEntry.respawnVariance ?? 0f;
            return Mathf.Max(0f, baseSeconds + Random.Range(-variance, variance));
        }
        return Mathf.Max(0f, respawnBase + Random.Range(-respawnVariance, respawnVariance));
    }
}
