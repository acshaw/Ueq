using Mirror;
using UnityEngine;

/// <summary>
/// Minimal "in combat" tracker (1.6.1). Server stamps it whenever this entity deals or takes damage
/// (hooked from <see cref="Health.TakeDamage"/>); a synced flag lets clients react (the HP-frame combat
/// indicator). Used by the camp gate, and a reusable foundation for later systems (rest/regen,
/// can't-zone-in-combat, etc.).
/// </summary>
public class CombatState : NetworkBehaviour
{
    const float CombatWindow = 10f; // decision D2

    [SyncVar(hook = nameof(OnInCombatChanged))]
    bool _inCombat;

    float _lastCombatTime;

    public bool InCombat => _inCombat;

    /// <summary>Fires on clients (and host) when combat state flips — drives the HP-frame indicator.</summary>
    public event System.Action<bool> OnCombatChanged;

    [Server]
    public void MarkCombat()
    {
        _lastCombatTime = Time.time;
        if (!_inCombat) _inCombat = true;
    }

    void Update()
    {
        if (!isServer) return;
        if (_inCombat && Time.time - _lastCombatTime >= CombatWindow)
            _inCombat = false;
    }

    void OnInCombatChanged(bool _, bool now) => OnCombatChanged?.Invoke(now);
}
