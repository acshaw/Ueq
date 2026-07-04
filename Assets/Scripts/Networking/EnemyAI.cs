using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NpcEventDispatcher))]
[RequireComponent(typeof(Health))]
public class EnemyAI : NetworkBehaviour, IOnAttacked, IOnDeath
{
    [Header("Combat")]
    [SerializeField] float attackRange    = 2f;
    [SerializeField] int   attackDamage   = 1;
    [SerializeField] float attackInterval = 2f;

    [Header("Aggro")]
    [SerializeField] int baseAggroThreat = 1;

    // resolved at OnStartServer from MobDefinition → serialized fallback
    float _attackRange;
    int   _attackDamage;
    float _attackInterval;
    int   _effectiveAggroThreat;

    public int BaseAggroThreat => _effectiveAggroThreat;

    enum State { Idle, Chase, Combat, Return }

    State                _state = State.Idle;
    NetworkIdentity      _currentTarget;
    Vector3              _spawnPoint;
    NavMeshAgent         _agent;
    Health               _health;
    NpcEventDispatcher   _dispatcher;
    INpcMovementBehavior _movementBehavior;

    readonly Dictionary<NetworkIdentity, int> _threatList = new();

    void Awake()
    {
        _agent            = GetComponent<NavMeshAgent>();
        _health           = GetComponent<Health>();
        _dispatcher       = GetComponent<NpcEventDispatcher>();
        _movementBehavior = GetComponent<INpcMovementBehavior>();

        // Movement is server-authoritative: the NavMeshAgent drives the transform only on the server,
        // and a NetworkTransform syncs it to clients. Start the agent disabled so on non-host clients it
        // never fights the NetworkTransform for control of the transform; OnStartServer re-enables it.
        if (_agent != null) _agent.enabled = false;
    }

    public override void OnStartServer()
    {
        // Resolve values after SpawnPoint may have called MobApplicator.SetDefinition
        var def = GetComponent<MobApplicator>()?.Definition;
        _attackRange          = def != null ? def.attackRange     : attackRange;
        _attackDamage         = def != null ? def.attackDamage    : attackDamage;
        _attackInterval       = def != null ? def.attackInterval  : attackInterval;
        _effectiveAggroThreat = def != null ? def.baseAggroThreat : baseAggroThreat;

        _spawnPoint       = transform.position;
        _movementBehavior = GetComponent<INpcMovementBehavior>(); // re-read: SetDefinition runs after Awake
        _agent.enabled    = true;
        _movementBehavior?.Startup();
    }

    // ── Public threat API ─────────────────────────────────────────────────────
    // Call this from any source: faction aggro, damage, spells, taunts, healer threat

    [Server]
    public void AddThreat(NetworkIdentity player, int amount)
    {
        if (_health.IsDead) { Debug.Log($"[EnemyAI] {name} AddThreat blocked — IsDead"); return; }
        if (_state == State.Return) { Debug.Log($"[EnemyAI] {name} AddThreat blocked — State.Return"); return; }
        if (player == null) return;

        if (!_threatList.ContainsKey(player))
            SubscribeToPlayerDeath(player);

        _threatList[player] = _threatList.GetValueOrDefault(player) + amount;

        var top = GetTopThreat();
        if (top != null && top != _currentTarget)
            SwitchTarget(top);
        else if (_currentTarget == null && top != null)
            SwitchTarget(top);
    }

    // ── IOnAttacked — damage dealt becomes threat ─────────────────────────────

    public void OnAttacked(int damage, NetworkIdentity attacker)
    {
        if (attacker != null)
            AddThreat(attacker, damage);
    }

    // ── IOnDeath — this NPC died ──────────────────────────────────────────────

    public void OnDeath(NetworkIdentity attacker)
    {
        _movementBehavior?.Suspend();
        StopAllCoroutines();
        ClearAllThreat();
        _agent.ResetPath();
        _agent.enabled = false;
        _state = State.Idle;
    }

    // ── State coroutines ──────────────────────────────────────────────────────

    IEnumerator ChaseLoop()
    {
        while (_state == State.Chase)
        {
            if (_currentTarget == null) { EnterReturn(); yield break; }

            float dist = Vector3.Distance(transform.position, _currentTarget.transform.position);
            if (dist <= _attackRange)
            {
                _state = State.Combat;
                _agent.ResetPath();
                StartCoroutine(CombatLoop());
                yield break;
            }

            TrySetDestination(_currentTarget.transform.position);
            yield return new WaitForSeconds(0.15f);
        }
    }

    IEnumerator CombatLoop()
    {
        while (_state == State.Combat)
        {
            if (_currentTarget == null) { EnterReturn(); yield break; }

            float dist = Vector3.Distance(transform.position, _currentTarget.transform.position);
            if (dist > _attackRange * 1.2f)
            {
                _state = State.Chase;
                StartCoroutine(ChaseLoop());
                yield break;
            }

            _currentTarget.GetComponent<Health>()?.TakeDamage(_attackDamage, netIdentity);

            yield return new WaitForSeconds(_attackInterval);
        }
    }

    IEnumerator ReturnLoop()
    {
        while (_state == State.Return)
        {
            if (Vector3.Distance(transform.position, _spawnPoint) <= _agent.stoppingDistance + 0.5f)
            {
                _health.ResetToFull();
                _state = State.Idle;
                _dispatcher.ResetPerception();
                _movementBehavior?.Resume();
                yield break;
            }
            yield return new WaitForSeconds(0.15f);
        }
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    void SwitchTarget(NetworkIdentity newTarget)
    {
        _currentTarget = newTarget;

        // If mid-combat, chase the new target
        if (_state == State.Combat)
        {
            StopAllCoroutines();
            _state = State.Chase;
            StartCoroutine(ChaseLoop());
        }
        else if (_state == State.Idle)
        {
            _movementBehavior?.Suspend();
            _state = State.Chase;
            StartCoroutine(ChaseLoop());
        }
        // Chase loop reads _currentTarget each tick — no restart needed
    }

    void EnterReturn()
    {
        _dispatcher.DispatchAggroLost();
        _currentTarget = null;
        _state = State.Return;
        TrySetDestination(_spawnPoint);
        StartCoroutine(ReturnLoop());
    }

    NetworkIdentity GetTopThreat()
    {
        NetworkIdentity top      = null;
        int             topValue = int.MinValue;

        foreach (var (ni, threat) in _threatList)
        {
            if (ni == null) continue;
            if (ni.GetComponent<Health>()?.IsDead == true) continue;
            if (threat > topValue) { topValue = threat; top = ni; }
        }

        return top;
    }

    void RemoveFromThreatList(NetworkIdentity player)
    {
        _threatList.Remove(player);

        if (player != _currentTarget) return;

        var next = GetTopThreat();
        if (next != null) SwitchTarget(next);
        else EnterReturn();
    }

    void ClearAllThreat()
    {
        _threatList.Clear();
        _currentTarget = null;
    }

    // Warp to nearest NavMesh point if the agent hasn't been placed yet, then set destination.
    // Guards against the one-frame window between _agent.enabled = true and NavMesh placement.
    void TrySetDestination(Vector3 destination)
    {
        if (!_agent.enabled) return;
        if (!_agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 3f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            else
                return;
        }
        _agent.SetDestination(destination);
    }

    void SubscribeToPlayerDeath(NetworkIdentity player)
    {
        var h = player.GetComponent<Health>();
        if (h == null) return;

        System.Action<NetworkIdentity> handler = null;
        handler = _ =>
        {
            h.OnDied -= handler;
            RemoveFromThreatList(player);
        };
        h.OnDied += handler;
    }

}
