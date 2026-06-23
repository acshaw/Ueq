using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

public class SpawnPoint : MonoBehaviour
{
    [SerializeField] SpawnTable spawnTable;
    [SerializeField] SpawnTimer timerOverride;   // overrides spawnTable.defaultTimer
    [SerializeField] float      activationRadius = 50f;

    [Header("Placement")]
    [Tooltip("Drop the spawn onto the terrain surface + navmesh so mobs sit on hills " +
             "instead of at the spawn point's raw Y. Disable for floating/aerial spawns.")]
    [SerializeField] bool      snapToGround = true;
    [Tooltip("Layers the terrain/ground colliders live on (for the downward surface raycast).")]
    [SerializeField] LayerMask groundMask   = ~0;
    [Tooltip("How far to search for the nearest navmesh point when snapping the spawn.")]
    [SerializeField] float     navSampleRadius = 8f;

    bool            _active;
    bool            _respawnPending;
    NetworkIdentity _live;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        InvokeRepeating(nameof(ActivationCheck), 0f, 5f);
    }

    // ── Activation ────────────────────────────────────────────────────────────

    void ActivationCheck()
    {
        if (!NetworkServer.active) return;

        bool hasPlayer = false;
        var cols = Physics.OverlapSphere(transform.position, activationRadius);
        foreach (var col in cols)
        {
            if (col.GetComponentInParent<NetworkedPlayer>() != null) { hasPlayer = true; break; }
        }

        _active = hasPlayer;

        if (_active && _live == null && !_respawnPending)
            DoSpawn();
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    void DoSpawn()
    {
        var entry = spawnTable?.Roll();
        if (entry?.mob?.prefab == null)
        {
            Debug.LogWarning($"[SpawnPoint] {name}: no valid entry to spawn.", this);
            return;
        }

        var go = Instantiate(entry.mob.prefab, ResolveSpawnPosition(), transform.rotation);
        go.GetComponent<MobApplicator>()?.SetDefinition(entry.mob);
        NetworkServer.Spawn(go);

        _live = go.GetComponent<NetworkIdentity>();

        var health = go.GetComponent<Health>();
        if (health != null)
        {
            System.Action<NetworkIdentity> handler = null;
            handler = _ => { health.OnDied -= handler; OnMobDied(); };
            health.OnDied += handler;
        }
    }

    // Resolve where the mob actually appears: drop straight down onto the terrain
    // surface below the spawn point, then snap onto the navmesh so the NavMeshAgent is
    // valid and sits at hill height (requires the navmesh to be baked over the hills).
    Vector3 ResolveSpawnPosition()
    {
        Vector3 pos = transform.position;
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

    void OnMobDied()
    {
        _live = null;

        if (_active)
            StartCoroutine(RespawnAfterDelay());
        // if not active: DoSpawn fires next time a player enters range
    }

    IEnumerator RespawnAfterDelay()
    {
        _respawnPending = true;
        var timer = timerOverride ?? spawnTable?.defaultTimer;
        float delay = timer != null ? timer.Roll() : 300f;
        yield return new WaitForSeconds(delay);
        _respawnPending = false;
        if (_active) DoSpawn();
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
