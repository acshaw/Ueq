using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class WanderBehavior : MonoBehaviour, INpcMovementBehavior
{
    [SerializeField] float wanderRadius   = 10f;
    [SerializeField] float wanderPauseMin = 2f;
    [SerializeField] float wanderPauseMax = 6f;

    NavMeshAgent _agent;
    Vector3      _spawnPoint;
    Coroutine    _wander;

    float _radius;
    float _pauseMin;
    float _pauseMax;

    void Awake() => _agent = GetComponent<NavMeshAgent>();

    public void Startup()
    {
        _spawnPoint = transform.position;

        var def = GetComponent<MobApplicator>()?.Definition;
        _radius   = def != null ? def.wanderRadius   : wanderRadius;
        _pauseMin = def != null ? def.wanderPauseMin : wanderPauseMin;
        _pauseMax = def != null ? def.wanderPauseMax : wanderPauseMax;

        Resume();
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

    IEnumerator WanderLoop()
    {
        while (true)
        {
            if (_agent.isActiveAndEnabled && _agent.isOnNavMesh)
            {
                var dest = RandomNavPoint(_spawnPoint, _radius);
                if (dest.HasValue)
                {
                    _agent.SetDestination(dest.Value);

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

    static Vector3? RandomNavPoint(Vector3 origin, float radius)
    {
        var candidate = origin + Random.insideUnitSphere * radius;
        candidate.y   = origin.y;
        return NavMesh.SamplePosition(candidate, out var hit, radius, NavMesh.AllAreas)
            ? hit.position
            : (Vector3?)null;
    }
}
