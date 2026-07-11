using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class SpawnPoint : MonoBehaviour
{
    [Tooltip("M2.7.2: spawn from a DB-backed spawn table (weighted entries + group size + timer), " +
             "resolved from SpawnTableRegistry. Highest precedence — the web-authored camp path.")]
    [SerializeField] string     spawnTableId = "";
    [Tooltip("M2.5: spawn a single DB-backed mob by its id (resolved from MobRegistry). Used when no " +
             "spawnTableId is set — for unique/named NPCs (e.g. a Merchant).")]
    [SerializeField] string     mobId = "";
    [SerializeField] float      activationRadius = 50f;

    [Header("Patrol (optional)")]
    [Tooltip("3.1.10: if set, spawned mobs patrol this route's ordered child waypoints instead of " +
             "wandering/standing. Leave empty for normal movement (wander/stationary per the mob definition).")]
    [SerializeField] PatrolRoute patrolRoute;

    [Header("Wander region (optional — ignored if a Patrol Route is set)")]
    [Tooltip("3.1.11: constrain wander mobs to this authored box/sphere area instead of a sphere around the " +
             "spawn point (leash). Ignored for stationary mobs and when a Patrol Route is set.")]
    [SerializeField] WanderRegion wanderRegion;
    [Tooltip("3.1.11: let wander mobs roam the whole zone (free-range) instead of a spawn leash. Ignored if a " +
             "Wander Region is set.")]
    [SerializeField] bool         freeRange;
    [Tooltip("Free-range roam spread. Kept well under the ~5000u zone spacing so mobs never wander into another zone.")]
    [SerializeField] float        freeRangeRadius = 400f;

    [Header("Placement")]
    [Tooltip("Drop the spawn onto the terrain surface + navmesh so mobs sit on hills " +
             "instead of at the spawn point's raw Y. Disable for floating/aerial spawns.")]
    [SerializeField] bool      snapToGround = true;
    [Tooltip("Layers the terrain/ground colliders live on (for the downward surface raycast).")]
    [SerializeField] LayerMask groundMask   = ~0;
    [Tooltip("How far to search for the nearest navmesh point when snapping the spawn.")]
    [SerializeField] float     navSampleRadius = 8f;

    // Read-only accessors for the editor scene-view labels (EncounterGizmos).
    public string SpawnTableId    => spawnTableId;
    public string MobId           => mobId;
    public bool   HasPatrol       => patrolRoute != null && patrolRoute.HasPoints;
    public bool   HasWanderRegion => wanderRegion != null;
    public bool   FreeRange       => freeRange;

    bool            _active;
    bool            _respawnPending;
    readonly List<NetworkIdentity> _live = new();   // M2.7.2: a group can have multiple live mobs
    SpawnTimer      _respawnTimer;                   // resolved at spawn, used for the respawn delay

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        InvokeRepeating(nameof(ActivationCheck), 0f, 5f);
    }

    // ── Activation ────────────────────────────────────────────────────────────

    void ActivationCheck()
    {
        if (!NetworkServer.active) return;

        _live.RemoveAll(x => x == null);   // prune any mob destroyed without firing OnDied

        bool hasPlayer = false;
        var cols = Physics.OverlapSphere(transform.position, activationRadius);
        foreach (var col in cols)
        {
            if (col.GetComponentInParent<NetworkedPlayer>() != null) { hasPlayer = true; break; }
        }

        _active = hasPlayer;

        if (_active && _live.Count == 0 && !_respawnPending)
            DoSpawn();
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    void DoSpawn()
    {
        // Precedence (M2.7.2): DB spawn table (weighted/timed/grouped) → single DB mob by id.
        MobDefinition def;
        int           groupSize;

        var table = SpawnTableRegistry.Get(spawnTableId);
        if (table != null)
        {
            var entry = table.Roll();
            def           = entry?.mob;
            groupSize     = Mathf.Max(1, entry?.groupSize ?? 1);
            _respawnTimer = table.defaultTimer;
        }
        else if (!string.IsNullOrEmpty(mobId))
        {
            def           = MobRegistry.Get(mobId);
            groupSize     = 1;
            _respawnTimer = null;
        }
        else
        {
            Debug.LogWarning($"[SpawnPoint] {name}: nothing configured to spawn " +
                             "(set spawnTableId or mobId).", this);
            return;
        }

        if (def?.prefab == null)
        {
            Debug.LogWarning($"[SpawnPoint] {name}: no valid mob to spawn " +
                             $"(spawnTableId='{spawnTableId}', mobId='{mobId}', prefab missing/unregistered).", this);
            return;
        }

        bool jitter = groupSize > 1;
        for (int i = 0; i < groupSize; i++)
            SpawnOne(def, jitter);
    }

    void SpawnOne(MobDefinition def, bool jitter)
    {
        var go = Instantiate(def.prefab, ResolveSpawnPosition(jitter), transform.rotation);

        // M3.0 Stage C: this spawn point belongs to a zone scene; keep its mobs in that scene so
        // SceneInterestManagement partitions them to that zone. Instantiate defaults to the active
        // scene (the base zone), so a spawn point in a non-base zone would otherwise leak mobs into
        // the base zone. Must happen before NetworkServer.Spawn (observers are computed on spawn).
        if (gameObject.scene.IsValid() && go.scene != gameObject.scene)
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);

        go.GetComponent<MobApplicator>()?.SetDefinition(def);

        // 3.1.10: turn this mob into a patroller if the spawn point has a route — swap the wander/stationary
        // behavior for a PatrolBehavior seeded with the route's world-space waypoints. Must run before Spawn so
        // EnemyAI.OnStartServer resolves the INpcMovementBehavior as the patrol (not the wander it just added).
        if (patrolRoute != null && patrolRoute.HasPoints)
            ApplyPatrol(go);
        else
            ApplyWanderRegion(go); // 3.1.11: configure the wander mode (no-op for the default spawn leash)

        NetworkServer.Spawn(go);

        var id = go.GetComponent<NetworkIdentity>();
        _live.Add(id);

        var health = go.GetComponent<Health>();
        if (health != null)
        {
            System.Action<NetworkIdentity> handler = null;
            handler = _ => { health.OnDied -= handler; OnMemberDied(id); };
            health.OnDied += handler;
        }
    }

    // Replace the mob's wander/stationary movement with a route patrol. DestroyImmediate (as MobApplicator
    // already uses at runtime) so GetComponent<INpcMovementBehavior> in EnemyAI.OnStartServer sees only the
    // PatrolBehavior. Each group member patrols the same route (spread by the spawn jitter).
    void ApplyPatrol(GameObject go)
    {
        var wander = go.GetComponent<WanderBehavior>();
        if (wander != null) DestroyImmediate(wander);
        var stationary = go.GetComponent<StationaryBehavior>();
        if (stationary != null) DestroyImmediate(stationary);

        var patrol = go.GetComponent<PatrolBehavior>() ?? go.AddComponent<PatrolBehavior>();
        patrol.SetRoute(patrolRoute.Points, patrolRoute.loop, patrolRoute.pausePerPoint);
    }

    // 3.1.11: configure a wander mob's roam region before it spawns. Bounded volume wins over free-range; neither
    // set = the default spawn leash (WanderBehavior left untouched). No-op for stationary mobs (no WanderBehavior).
    void ApplyWanderRegion(GameObject go)
    {
        if (wanderRegion == null && !freeRange) return;      // default leash — nothing to change
        var wander = go.GetComponent<WanderBehavior>();
        if (wander == null) return;                          // stationary mob — nothing to constrain
        if (wanderRegion != null) wander.SetBoundedRegion(wanderRegion);
        else                      wander.SetFreeRange(freeRangeRadius);
    }

    // Resolve where the mob actually appears: drop straight down onto the terrain
    // surface below the spawn point, then snap onto the navmesh so the NavMeshAgent is
    // valid and sits at hill height (requires the navmesh to be baked over the hills).
    Vector3 ResolveSpawnPosition(bool jitter = false)
    {
        Vector3 pos = transform.position;
        // Spread group members so they don't spawn perfectly stacked.
        if (jitter)
        {
            var off = Random.insideUnitCircle * 2.5f;
            pos += new Vector3(off.x, 0f, off.y);
        }
        if (!snapToGround) return pos;

        // 1) Find the terrain surface directly below this XZ.
        Vector3 origin = pos + Vector3.up * 50f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 100f, groundMask, QueryTriggerInteraction.Ignore))
            pos = hit.point;

        // 2) Snap to the nearest navmesh point so the agent has somewhere valid to stand.
        if (NavMesh.SamplePosition(pos, out var navHit, navSampleRadius, NavMesh.AllAreas))
            pos = navHit.position;

        return pos;
    }

    void OnMemberDied(NetworkIdentity id)
    {
        _live.Remove(id);
        if (_live.Count > 0) return;   // wait for the rest of the group to die

        if (_active)
            StartCoroutine(RespawnAfterDelay());
        // if not active: DoSpawn fires next time a player enters range
    }

    IEnumerator RespawnAfterDelay()
    {
        _respawnPending = true;
        float delay = _respawnTimer != null ? _respawnTimer.Roll() : 300f;
        yield return new WaitForSeconds(delay);
        _respawnPending = false;
        if (_active && _live.Count == 0) DoSpawn();
    }

    // ── Editor gizmo ──────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}
