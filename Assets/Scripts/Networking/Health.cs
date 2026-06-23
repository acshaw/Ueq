using Mirror;
using UnityEngine;

// Attach to any entity that can take damage — player or enemy.
public class Health : NetworkBehaviour
{
    [SerializeField] int maxHealth = 100;

    [SyncVar(hook = nameof(OnHealthChanged))]
    int _current;

    [SyncVar(hook = nameof(OnMaxChanged))]
    int _maxSync;

    MobApplicator    _mob;
    CharacterStats   _stats;
    PlayerExperience _exp;
    float            _immunityUntil;

    int EffectiveMax
    {
        get
        {
            if (_stats != null && _exp != null)
            {
                var cls = _exp.CurrentClass;
                if (cls != null)
                {
                    int level = _exp.Level;
                    int effectiveSta = Mathf.Min(_stats.Sta, cls.staCap);
                    float staModifier = cls.baseStaRatio + (level - 1) * cls.staGrowthRate;
                    return cls.classBaseHP + (level - 1) * cls.hpPerLevel + Mathf.RoundToInt(effectiveSta * staModifier);
                }
            }
            if (_mob != null && _mob.Definition != null) return _mob.Definition.maxHealth;
            return maxHealth;
        }
    }

    public int Current => _current;
    public int Max     => isServer ? EffectiveMax : _maxSync;
    public bool IsDead => _current <= 0;

    // Server-side events — subscribe in OnStartServer
    public event System.Action<int, NetworkIdentity> OnDamaged;  // (amount, attacker)
    public event System.Action<NetworkIdentity> OnDied;           // attacker (may be null)

    // Client-side events — subscribe on clients for UI / VFX
    public event System.Action<int, int> OnHealthUpdated;        // (current, max)

    void Awake()
    {
        _mob   = GetComponent<MobApplicator>();
        _stats = GetComponent<CharacterStats>();
        _exp   = GetComponent<PlayerExperience>();
    }

    public override void OnStartServer()
    {
        _maxSync = EffectiveMax;
        _current = EffectiveMax;
    }

    public bool IsImmune => Time.time < _immunityUntil;

    [Server]
    public void SetImmunity(float seconds) => _immunityUntil = Time.time + seconds;

    [Server]
    public void RefreshMax()
    {
        int newMax = EffectiveMax;
        _maxSync = newMax;
        if (_current > newMax) _current = newMax;
    }

    [Server]
    public void TakeDamage(int amount, NetworkIdentity attacker = null)
    {
        if (IsDead) return;
        if (Time.time < _immunityUntil) return;

        // Combat-state stamp (1.6.1) — all damage routes through here, so this covers both the victim
        // (taking) and the attacker (dealing). Only players carry CombatState; null-safe for mobs.
        GetComponent<CombatState>()?.MarkCombat();
        if (attacker != null) attacker.GetComponent<CombatState>()?.MarkCombat();

        _current = Mathf.Max(0, _current - amount);
        if (IsDead)
        {
            OnDied?.Invoke(attacker);
            RpcOnDied();
        }
        else
        {
            OnDamaged?.Invoke(amount, attacker);
        }
    }

    [Server]
    public void Heal(int amount)
    {
        if (IsDead) return;
        _current = Mathf.Min(EffectiveMax, _current + amount);
    }

    [Server]
    public void ResetToFull()
    {
        _maxSync = EffectiveMax;
        _current = EffectiveMax;
    }

    /// <summary>Restore current HP from a loaded snapshot. Call AFTER <see cref="RefreshMax"/> so the
    /// value clamps against the correct max (1.3).</summary>
    [Server]
    public void SetCurrent(int value) => _current = Mathf.Clamp(value, 0, EffectiveMax);

    void OnHealthChanged(int _, int newVal) => OnHealthUpdated?.Invoke(newVal, Max);
    void OnMaxChanged(int _, int __)        => OnHealthUpdated?.Invoke(_current, Max);

    [ClientRpc]
    void RpcOnDied() { }
}
