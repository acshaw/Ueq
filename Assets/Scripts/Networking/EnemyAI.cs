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

    // 5.4 (AG4) — a threat entry tracks damage AND status separately: Active entries are live combat
    // threats (drive retargeting); Dead/Zoned entries are departed players whose damage still counts
    // toward 5.3's kill-credit resolution, but who are never re-targeted and never keep the mob engaged.
    public enum ThreatStatus { Active, Dead, Zoned }

    class ThreatEntry
    {
        public int Damage;
        public ThreatStatus Status = ThreatStatus.Active;
    }

    readonly Dictionary<NetworkIdentity, ThreatEntry> _threatList = new();

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

        if (_threatList.TryGetValue(player, out var entry))
        {
            entry.Damage += amount; // status stays whatever it was (realistically always Active — a
                                     // dead/zoned identity can't act, so this path won't re-fire for them)
        }
        else
        {
            entry = new ThreatEntry { Damage = amount, Status = ThreatStatus.Active };
            _threatList[player] = entry;
            // 5.4 (AG4) — reverse-index registration, replacing the old per-mob OnDied subscription: lets
            // the player proactively tell every mob that has them threat-listed when they depart (death/
            // zone), instead of each mob independently subscribing to that player's own death event.
            player.GetComponent<NetworkedPlayer>()?.RegisterThreateningMob(this);
        }

        var top = GetTopThreat();
        if (top != null && top != _currentTarget)
            SwitchTarget(top);
        else if (_currentTarget == null && top != null)
            SwitchTarget(top);
    }

    /// <summary>5.4 (AG4) — called by a departing player (NetworkedPlayer) on every mob that has them
    /// threat-listed. Flips status rather than removing the entry, so 5.3's ResolveCreditedGroup can still
    /// credit their damage at eventual kill time. If they were the current target and no Active threat
    /// remains, reassess/return exactly like the old removal path did.</summary>
    [Server]
    public void MarkThreatStatus(NetworkIdentity player, ThreatStatus status)
    {
        if (player == null || !_threatList.TryGetValue(player, out var entry)) return;
        entry.Status = status;

        if (player != _currentTarget) return;

        var next = GetTopThreat();
        if (next != null) SwitchTarget(next);
        else EnterReturn();
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
        // 5.3 (GP5): _threatList is deliberately NOT cleared here — it holds each attacker's final damage
        // total, which ResolveCreditedGroup (called from MobKillReward.OnDeath, another IOnDeath subscriber
        // on this same GameObject) needs to read. Leaving it intact makes that read correct regardless of
        // which of the two OnDeath calls the NpcEventDispatcher fan-out happens to run first. AddThreat
        // already refuses to add anything once _health.IsDead, so nothing repopulates it after this point.
        _currentTarget = null;
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
                BroadcastSocialAggro(); // 5.4 (AG3) — edge-triggered, once per Chase→Combat transition
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

        // 2026-08-13 follow-up — same defender-side training as PlayerAutoAttack's player-vs-mob path.
        var defenderAvoidance = _currentTarget.GetComponent<PlayerAvoidanceSkills>();
        if (defenderAvoidance != null)
        {
            defenderAvoidance.RollDefenseUp();
            defenderAvoidance.RollDodgeUp();
            defenderAvoidance.RollRiposteUp();
            if (ctx.IsParryable) defenderAvoidance.RollParryUp();
        }

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

                // 5.4 (AG4) — fully disengaged: wipe the whole list (including Dead/Zoned entries kept
                // around for kill-credit purposes) so a completely unrelated future encounter starts
                // genuinely fresh instead of inheriting stale history.
                foreach (var ni in _threatList.Keys)
                    ni?.GetComponent<NetworkedPlayer>()?.UnregisterThreateningMob(this);
                _threatList.Clear();

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

    // 5.4 (AG4) — only Active entries are live combat threats; Dead/Zoned entries are kept (for 5.3's
    // kill-credit purposes, see ResolveCreditedGroup) but never picked here. A departed player's identity
    // going null (e.g. disconnect, which destroys the character) is also naturally skipped — no explicit
    // handling needed for that case, Unity's overridden null-equality on a destroyed Object already covers
    // it, same as the coroutines' own `_currentTarget == null` checks.
    NetworkIdentity GetTopThreat()
    {
        NetworkIdentity top      = null;
        int             topValue = int.MinValue;

        foreach (var (ni, entry) in _threatList)
        {
            if (ni == null) continue;
            if (entry.Status != ThreatStatus.Active) continue;
            if (entry.Damage > topValue) { topValue = entry.Damage; top = ni; }
        }

        return top;
    }

    // 5.4 (AG3) — social aggro: pulls in nearby same-faction-or-allied mobs when this mob enters Combat,
    // if socialAggroEnabled (opt-in per mob, default off). Each eligible ally adds threat against the SAME
    // target directly (skipping its own independent perception check), per the design doc's spec.
    [Server]
    void BroadcastSocialAggro()
    {
        var def = GetComponent<MobApplicator>()?.Definition;
        if (def == null || !def.socialAggroEnabled || _currentTarget == null) return;

        var myFaction = GetComponent<NpcFaction>()?.Faction;
        if (myFaction == null) return;

        var notified = new HashSet<EnemyAI>();
        var hits = Physics.OverlapSphere(transform.position, def.socialAggroRadius);
        foreach (var hit in hits)
        {
            var otherAI = hit.GetComponentInParent<EnemyAI>();
            if (otherAI == null || otherAI == this || !notified.Add(otherAI)) continue;

            var theirFaction = otherAI.GetComponent<NpcFaction>()?.Faction;
            if (theirFaction == null) continue;
            if (theirFaction != myFaction && !myFaction.IsAllyWith(theirFaction)) continue;

            otherAI.AddThreat(_currentTarget, otherAI.BaseAggroThreat);
        }
    }

    // ── 5.3 (GP5) — multi-group kill-credit resolution ────────────────────────
    // Groups this mob's final threat tally (damage dealt, per the shared aggro-is-damage model) by party —
    // an ungrouped attacker counts as its own party of one — and returns the full member list of whichever
    // group dealt the most damage. Used by MobKillReward for both XP credit and (via Corpse.
    // SetEligibleLooters) loot rights. Reads _threatList directly; OnDeath (above) deliberately leaves it
    // intact so this works no matter which order IOnDeath subscribers on this GameObject run in. Ties
    // (exact equal damage, rare) go to the killing blow's group.
    [Server]
    public List<NetworkIdentity> ResolveCreditedGroup(NetworkIdentity killingBlowAttacker)
    {
        var groupDamage = new Dictionary<uint, int>();
        var soloDamage  = new Dictionary<NetworkIdentity, int>();

        // 5.4 (AG4): sums Damage across EVERY entry regardless of status — a dead/zoned group member's
        // contribution still counts toward their group winning the majority-damage contest; their personal
        // XP share is still separately gated by 5.3's own same-zone/in-range filter (GP4).
        foreach (var (ni, entry) in _threatList)
        {
            if (ni == null) continue;
            uint partyId = ni.GetComponent<PlayerParty>()?.PartyId ?? 0;
            if (partyId != 0) groupDamage[partyId] = groupDamage.GetValueOrDefault(partyId) + entry.Damage;
            else               soloDamage[ni]      = soloDamage.GetValueOrDefault(ni) + entry.Damage;
        }

        uint            bestPartyId = 0;
        NetworkIdentity bestSolo    = null;
        int             bestDamage  = int.MinValue;

        foreach (var (id, dmg) in groupDamage)
            if (dmg > bestDamage) { bestDamage = dmg; bestPartyId = id; bestSolo = null; }
        foreach (var (ni, dmg) in soloDamage)
            if (dmg > bestDamage) { bestDamage = dmg; bestPartyId = 0; bestSolo = ni; }

        if (killingBlowAttacker != null)
        {
            uint killerPartyId = killingBlowAttacker.GetComponent<PlayerParty>()?.PartyId ?? 0;
            int  killerDamage  = killerPartyId != 0
                ? groupDamage.GetValueOrDefault(killerPartyId)
                : soloDamage.GetValueOrDefault(killingBlowAttacker);

            if (killerDamage == bestDamage)
            {
                bestPartyId = killerPartyId;
                bestSolo    = killerPartyId != 0 ? null : killingBlowAttacker;
            }
        }

        List<NetworkIdentity> result = bestPartyId != 0
            ? (PartyManager.Instance?.MembersOf(bestPartyId) ?? new List<NetworkIdentity>())
            : (bestSolo != null ? new List<NetworkIdentity> { bestSolo } : new List<NetworkIdentity>());

        // Safety net — should only ever trigger if something bypassed the normal OnAttacked→AddThreat path
        // (e.g. a non-combat death), not the ordinary case (the killing blow always leaves a threat entry).
        if (result.Count == 0 && killingBlowAttacker != null)
            result.Add(killingBlowAttacker);

        return result;
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
}
