using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Idle wandering: repeatedly pick a random navmesh point and walk there, pausing between moves. 3.1.11 made the
/// "where may I roam" question pluggable via <see cref="IWanderRegion"/> — leashed to spawn (default, unchanged),
/// bounded to an authored <see cref="WanderRegion"/> volume, or free-range across the zone. The loop is identical
/// across all three; only the region's sampling differs. Regions never touch chase/aggro — idle wander only.
/// </summary>
public class WanderBehavior : MonoBehaviour, INpcMovementBehavior
{
    [SerializeField] float wanderRadius   = 10f;
    [SerializeField] float wanderPauseMin = 2f;
    [SerializeField] float wanderPauseMax = 6f;

    NavMeshAgent _agent;
    Coroutine    _wander;

    float _pauseMin;
    float _pauseMax;

    IWanderRegion _region;              // built in Startup from the mode below
    WanderRegion  _boundedRegion;       // optional authored volume (set by SpawnPoint before spawn)
    bool          _freeRange;           // roam the whole zone (set by SpawnPoint before spawn)
    float         _freeRangeRadius = 400f;

    void Awake() => _agent = GetComponent<NavMeshAgent>();

    // Injected by SpawnPoint before NetworkServer.Spawn (mirrors PatrolBehavior.SetRoute), so Startup sees them.
    // A bounded region wins over free-range; neither set = the default spawn leash.
    public void SetBoundedRegion(WanderRegion region) => _boundedRegion = region;
    public void SetFreeRange(float radius) { _freeRange = true; if (radius > 0f) _freeRangeRadius = radius; }

    public void Startup()
    {
        var def = GetComponent<MobApplicator>()?.Definition;
        float radius = def != null ? def.wanderRadius   : wanderRadius;
        _pauseMin    = def != null ? def.wanderPauseMin : wanderPauseMin;
        _pauseMax    = def != null ? def.wanderPauseMax : wanderPauseMax;

        _region = BuildRegion(transform.position, radius);
        Resume();
    }

    IWanderRegion BuildRegion(Vector3 spawn, float leashRadius)
    {
        if (_boundedRegion != null) return new BoundedRegion(_boundedRegion);
        if (_freeRange)             return new ZoneRegion(spawn, _freeRangeRadius);
        return new SpawnAnchoredRegion(spawn, leashRadius); // default = pre-3.1.11 behavior, unchanged
    }

    public void Suspend()
    {
        if (_wander != null) { StopCoroutine(_wander); _wander = null; }
        _agent.ResetPath();
    }

    public void Resume()
    {
        if (_wander != null) StopCoroutine(_wander);
        _wander = StartCoroutine(WanderLoop());
    }

    public Vector3 GetReturnAnchor(Vector3 spawnPoint)
        => _region != null ? _region.GetReturnAnchor(transform.position) : spawnPoint;

    IEnumerator WanderLoop()
    {
        while (true)
        {
            if (_agent.isActiveAndEnabled && _agent.isOnNavMesh && _region != null)
            {
                if (_region.TryGetRandomPoint(out var dest))
                {
                    _agent.SetDestination(dest);

                    // Wait until arrived or 10s timeout — guards against stuck paths
                    float elapsed = 0f;
                    while (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance)
                    {
                        elapsed += Time.deltaTime;
                        if (elapsed >= 10f) break;
                        yield return null;
                    }
                }
            }

            yield return new WaitForSeconds(Random.Range(_pauseMin, _pauseMax));
        }
    }
}
