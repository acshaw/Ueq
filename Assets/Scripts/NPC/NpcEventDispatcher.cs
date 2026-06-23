using System.Collections.Generic;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class NpcEventDispatcher : NetworkBehaviour
{
    [Header("Perception")]
    [SerializeField] float perceptionRadius   = 20f;
    [SerializeField] float perceptionInterval = 0.5f;

    [Header("Timer")]
    [SerializeField] float timerInterval = 0f; // 0 = disabled

    MobApplicator _mob;
    NpcFaction    _myFaction;
    EnemyAI       _myAI;

    IOnSpawned[]             _spawned;
    IOnPerceived[]           _perceived;
    IOnTargeted[]            _targeted;
    IOnConversationKeyword[] _keyword;
    IOnAttacked[]            _attacked;
    IOnFactionChanged[]      _factionChanged;
    IOnAggroLost[]           _aggroLost;
    IOnDeath[]               _death;
    IOnConversationStart[]   _convStart;
    IOnConversationEnd[]     _convEnd;
    IOnTimer[]               _timer;

    Health _health;
    readonly HashSet<NetworkIdentity> _perceivedPlayers = new();

    void Awake()
    {
        _mob       = GetComponent<MobApplicator>();
        _health    = GetComponent<Health>();
        _myFaction = GetComponent<NpcFaction>();
        _myAI      = GetComponent<EnemyAI>();
        _spawned        = GetComponents<IOnSpawned>();
        _perceived      = GetComponents<IOnPerceived>();
        _targeted       = GetComponents<IOnTargeted>();
        _keyword        = GetComponents<IOnConversationKeyword>();
        _attacked       = GetComponents<IOnAttacked>();
        _factionChanged = GetComponents<IOnFactionChanged>();
        _aggroLost      = GetComponents<IOnAggroLost>();
        _death          = GetComponents<IOnDeath>();
        _convStart      = GetComponents<IOnConversationStart>();
        _convEnd        = GetComponents<IOnConversationEnd>();
        _timer          = GetComponents<IOnTimer>();
    }

    public override void OnStartServer()
    {
        _health.OnDamaged -= OnHealthDamaged;
        _health.OnDied    -= OnHealthDied;
        _health.OnDamaged += OnHealthDamaged;
        _health.OnDied    += OnHealthDied;

        foreach (var l in _spawned) l.OnSpawned();

        InvokeRepeating(nameof(PerceptionTick), perceptionInterval, perceptionInterval);
        if (timerInterval > 0f)
            InvokeRepeating(nameof(TimerTick), timerInterval, timerInterval);
    }

    public override void OnStopServer()
    {
        _health.OnDamaged -= OnHealthDamaged;
        _health.OnDied    -= OnHealthDied;
        CancelInvoke();
    }

    // ── Health hooks ──────────────────────────────────────────────────────────

    void OnHealthDamaged(int amount, NetworkIdentity attacker)
    {
        foreach (var l in _attacked) l.OnAttacked(amount, attacker);
    }

    void OnHealthDied(NetworkIdentity attacker)
    {
        foreach (var l in _death) l.OnDeath(attacker);
        _perceivedPlayers.Clear();
    }

    // ── Perception ────────────────────────────────────────────────────────────

    [Server]
    void PerceptionTick()
    {
        if (_health.IsDead) return;

        float radius = _mob?.Definition != null ? _mob.Definition.perceptionRadius : perceptionRadius;
        var cols = Physics.OverlapSphere(transform.position, radius);
        var inRange = new HashSet<NetworkIdentity>();

        foreach (var col in cols)
        {
            var player = col.GetComponentInParent<NetworkedPlayer>();
            if (player == null) continue;
            var ni = player.GetComponent<NetworkIdentity>();
            if (ni != null) inRange.Add(ni);
        }

        foreach (var ni in inRange)
        {
            if (_perceivedPlayers.Add(ni))
            {
                float dist = Vector3.Distance(transform.position, ni.transform.position);
                foreach (var l in _perceived) l.OnPerceived(ni, dist);
            }
        }

        _perceivedPlayers.RemoveWhere(ni => !inRange.Contains(ni));

        // NPC-to-NPC hostility — no edge trigger; AddThreat every tick so the guard
        // re-engages after returning even if the rat drifted outside perception radius.
        if (_myFaction?.Faction != null && _myAI != null)
        {
            foreach (var col in cols)
            {
                var otherFaction = col.GetComponentInParent<NpcFaction>();
                if (otherFaction == null || otherFaction == _myFaction) continue;
                if (!_myFaction.Faction.IsHostileWith(otherFaction.Faction)) continue;
                var otherNi = otherFaction.GetComponent<NetworkIdentity>();
                if (otherNi != null) _myAI.AddThreat(otherNi, _myAI.BaseAggroThreat);
            }
        }
    }

    [Server]
    void TimerTick()
    {
        foreach (var l in _timer) l.OnTimer();
    }

    // ── Public dispatch API ───────────────────────────────────────────────────

    // Client — fires on whichever side calls it (targeting is client-side)
    public void DispatchTargeted(NetworkIdentity player)
    {
        foreach (var l in _targeted) l.OnTargeted(player);
    }

    public void DispatchConversationKeyword(NetworkIdentity player, string keyword)
    {
        if (!isServer) return;
        foreach (var l in _keyword) l.OnConversationKeyword(player, keyword);
    }

    public void DispatchFactionChanged(NetworkIdentity player, int oldScore, int newScore)
    {
        if (!isServer) return;
        foreach (var l in _factionChanged) l.OnFactionChanged(player, oldScore, newScore);
    }

    public void DispatchAggroLost()
    {
        if (!isServer) return;
        foreach (var l in _aggroLost) l.OnAggroLost();
    }

    // Call when an NPC returns to idle so perception edge-triggers reset and re-aggro works
    public void ResetPerception()
    {
        if (!isServer) return;
        _perceivedPlayers.Clear();
    }

    public void DispatchConversationStart(NetworkIdentity player)
    {
        if (!isServer) return;
        foreach (var l in _convStart) l.OnConversationStart(player);
    }

    public void DispatchConversationEnd(NetworkIdentity player)
    {
        if (!isServer) return;
        foreach (var l in _convEnd) l.OnConversationEnd(player);
    }
}
