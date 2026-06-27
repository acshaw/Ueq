using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

public class SpawnPoint : MonoBehaviour
{
    [Tooltip("M2.7.2: spawn from a DB-backed spawn table (weighted entries + group size + timer), " +
             "resolved from SpawnTableRegistry. Highest precedence — the web-authored camp path.")]
    [SerializeField] string     spawnTableId = "";
    [Tooltip("M2.5: spawn a single DB-backed mob by its id (resolved from MobRegistry). Used when no " +
             "spawnTableId is set — for unique/named NPCs (e.g. a Merchant).")]
    [SerializeField] string     mobId = "";
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
        go.GetComponent<MobApplicator>()?.SetDefinition(def);
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
