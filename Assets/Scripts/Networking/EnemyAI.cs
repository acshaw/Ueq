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

    [Header("Facing")]
    [SerializeField] float turnSpeed = 540f; // deg/sec — server-side facing toward the target while in Combat

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
    readonly List<NetworkIdentity>            _zonedOut   = new(); // WR7 scratch — reused each prune, no alloc

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

        // Sync rotation so remote clients see the mob turn (chase via the agent, combat via Update below).
        // The prefab ships syncRotation off; override it here on every peer — the format must match on both
        // sides, and Awake runs before spawn. (Same pattern NetworkedPlayer uses for player yaw.)
        var nt = GetComponent<NetworkTransformBase>();
        if (nt != null) nt.syncRotation = true;
    }

    // Server-only: while attacking (agent path reset → no auto-rotation) turn to face the current target so
    // remote clients see the mob look at whoever it's fighting. During Chase the agent handles facing.
    void Update()
    {
        if (!isServer || _agent == null || !_agent.enabled) return;

        PruneZonedOutThreats(); // WR7 — drop targets that changed zone (scene)

        if (_state == State.Combat && _currentTarget != null)
        {
            if (_agent.updateRotation) _agent.updateRotation = false;

            Vector3 to = _currentTarget.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                Quaternion goal = Quaternion.LookRotation(to);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, turnSpeed * Time.deltaTime);
            }
        }
        else if (!_agent.updateRotation)
        {
            _agent.updateRotation = true; // hand facing back to the agent for Chase/Return/Idle movement
        }
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

            ResolveAttack();

            yield return new WaitForSeconds(_attackInterval);
        }
    }

    // 5.1.1-5.1.4: mob → player swing through the shared combat pipeline (symmetric with
    // PlayerAutoAttack's player → mob swing — same CombatResolver, same four steps).
    void ResolveAttack()
    {
        var targetHealth = _currentTarget.GetComponent<Health>();
        if (targetHealth == null || targetHealth.IsDead) return;

        var def = GetComponent<MobApplicator>()?.Definition;
        var cat = def != null ? def.weaponCategory : WeaponCategory.Might;

        var ctx = new CombatResolver.AttackContext
        {
            Attacker         = CombatResolver.BuildCombatant(gameObject, cat),
            Defender         = CombatResolver.BuildCombatant(_currentTarget.gameObject, cat),
            IsRearAttack     = CombatResolver.IsRearAttack(transform, _currentTarget.transform),
            IsParryable      = def == null || def.attackIsParryable,
            WeaponBaseDamage = _attackDamage,
            RelevantStat     = 0f, // mobs have no CharacterStats — attackDamage already represents full power
        };
        var result = CombatResolver.ResolveAttack(ctx);

        var playerConn = _currentTarget.connectionToClient;
        if (result.Tier == HitTier.Miss)
        {
            if (result.Riposted)
            {
                _health.TakeDamage(result.RiposteDamage, _currentTarget);
                ChatManager.Instance?.SendDirect(
                    new ChatMessage(ChatChannel.Combat, "", $"You riposte {name}'s attack!"), playerConn);
            }
            else
            {
                ChatManager.Instance?.SendDirect(
                    new ChatMessage(ChatChannel.Combat, "", $"{name}'s attack misses you."), playerConn);
            }
        }
        else
        {
            targetHealth.TakeDamage(result.Damage, netIdentity);
        }
    }

    // WR5: the return anchor is the movement behavior's call — spawn (leash → walk home) for a leashed/patrol/
    // stationary mob, or the mob's current position (reset in place) for a free-range/bounded roamer.
    IEnumerator ReturnLoop(Vector3 anchor)
    {
        while (_state == State.Return)
        {
            if (Vector3.Distance(transform.position, anchor) <= _agent.stoppingDistance + 0.5f)
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
        Vector3 anchor = _movementBehavior != null ? _movementBehavior.GetReturnAnchor(_spawnPoint) : _spawnPoint;
        TrySetDestination(anchor);
        StartCoroutine(ReturnLoop(anchor));
    }

    // WR7: a player who zones is moved to another scene (~5000u away) without being destroyed, so _currentTarget
    // stays non-null and the mob would otherwise chase toward the far-off position forever. Treat "target in a
    // different scene" as target-loss. Prune all zoned-out entries first, then reassess ONCE via the existing
    // threat-list logic (next same-zone threat, else return) — the same coroutine-safe path player-death uses,
    // called from Update (outside the movement coroutines). No fresh perception scan for a new bystander (that's
    // 3.4). Runs while the mob is alive + active (Update's guards), so it's cheap and idle mobs skip it (no threat).
    void PruneZonedOutThreats()
    {
        if (_threatList.Count == 0) return;

        _zonedOut.Clear();
        foreach (var ni in _threatList.Keys)
            if (ni == null || ni.gameObject.scene != gameObject.scene)
                _zonedOut.Add(ni);
        if (_zonedOut.Count == 0) return;

        bool droppedCurrent = false;
        foreach (var ni in _zonedOut)
        {
            _threatList.Remove(ni);
            if (_currentTarget != null && ni == _currentTarget) droppedCurrent = true;
        }

        // Only reassess if the mob was actively engaged with a target that just left the zone. If it was already
        // returning/idle (_currentTarget null), pruning stale entries is enough — don't spuriously re-engage.
        if (!droppedCurrent) return;

        var next = GetTopThreat();
        if (next != null) SwitchTarget(next);
        else              EnterReturn();
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
