using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 3.1.10 Stage 1 — walks an ordered set of world-space waypoints instead of random-wandering. A drop-in
/// <see cref="INpcMovementBehavior"/> sibling to <see cref="WanderBehavior"/>: <c>EnemyAI</c> drives movement
/// purely through the interface (Startup on spawn, Suspend on combat/death, Resume on returning to idle), so no
/// AI changes are needed. Seeded by <c>SpawnPoint</c> via <see cref="SetRoute"/> before the mob is spawned.
///
/// After a fight, <c>EnemyAI</c> returns to its spawn then calls <see cref="Resume"/>; the patrol re-acquires
/// the nearest waypoint so the guard walks back onto its beat rather than snapping to the route's start.
/// </summary>
public class PatrolBehavior : MonoBehaviour, INpcMovementBehavior
{
    NavMeshAgent _agent;
    Vector3[]    _points;
    bool         _loop  = true;
    float        _pause = 2f;

    int          _index;
    int          _dir = 1;     // ping-pong direction when !_loop
    Coroutine    _patrol;

    void Awake() => _agent = GetComponent<NavMeshAgent>();

    // Injected by SpawnPoint (before NetworkServer.Spawn) from a PatrolRoute's child waypoints.
    public void SetRoute(Vector3[] points, bool loop, float pausePerPoint)
    {
        _points = points;
        _loop   = loop;
        _pause  = pausePerPoint;
    }

    public void Startup()
    {
        _index = NearestIndex();
        Resume();
    }

    public void Suspend()
    {
        if (_patrol != null) { StopCoroutine(_patrol); _patrol = null; }
        if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh) _agent.ResetPath();
    }

    public void Resume()
    {
        if (_points == null || _points.Length == 0) return;
        _index = NearestIndex();  // resume onto the nearest point after combat/return
        if (_patrol != null) StopCoroutine(_patrol);
        _patrol = StartCoroutine(PatrolLoop());
    }

    // 3.1.11 (WR5): patrol keeps the pre-existing "walk back to spawn, then Resume re-acquires the nearest
    // waypoint" behavior — return the spawn point unchanged.
    public Vector3 GetReturnAnchor(Vector3 spawnPoint) => spawnPoint;

    IEnumerator PatrolLoop()
    {
        while (true)
        {
            if (_agent.isActiveAndEnabled && _agent.isOnNavMesh && _points != null && _points.Length > 0)
            {
                if (NavMesh.SamplePosition(_points[_index], out var hit, 4f, NavMesh.AllAreas))
                    _agent.SetDestination(hit.position);

                // Wait until arrived or a 15s timeout — guards against an unreachable point.
                float elapsed = 0f;
                while (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance + 0.1f)
                {
                    elapsed += Time.deltaTime;
                    if (elapsed >= 15f) break;
                    yield return null;
                }

                yield return new WaitForSeconds(_pause);
                Advance();
            }
            else
            {
                yield return null;
            }
        }
    }

    void Advance()
    {
        if (_points.Length <= 1) return;

        if (_loop)
        {
            _index = (_index + 1) % _points.Length;
        }
        else // ping-pong: reverse at the ends
        {
            if (_index + _dir < 0 || _index + _dir >= _points.Length) _dir = -_dir;
            _index += _dir;
        }
    }

    int NearestIndex()
    {
        if (_points == null || _points.Length == 0) return 0;

        int   best    = 0;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < _points.Length; i++)
        {
            float d = (_points[i] - transform.position).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = i; }
        }
        return best;
    }
}
