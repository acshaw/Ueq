using Mirror;
using UnityEngine;

// Attach to any entity that can take damage — player or enemy.
public class Health : NetworkBehaviour
{
    [SerializeField] int maxHealth = 100;

    [SyncVar(hook = nameof(OnHealthChanged))]
    int _current;

    public int Current => _current;
    public int Max => maxHealth;
    public bool IsDead => _current <= 0;

    // Subscribe to these on client for UI / VFX
    public event System.Action<int, int> OnHealthUpdated;  // (current, max)
    public event System.Action<NetworkIdentity> OnDied;    // attacker (may be null)

    public override void OnStartServer() => _current = maxHealth;

    [Server]
    public void TakeDamage(int amount, NetworkIdentity attacker = null)
    {
        if (IsDead) return;
        _current = Mathf.Max(0, _current - amount);
        if (IsDead) RpcDied(attacker);
    }

    [Server]
    public void Heal(int amount)
    {
        if (IsDead) return;
        _current = Mathf.Min(maxHealth, _current + amount);
    }

    void OnHealthChanged(int _, int newVal)
    {
        OnHealthUpdated?.Invoke(newVal, maxHealth);
    }

    [ClientRpc]
    void RpcDied(NetworkIdentity attacker)
    {
        OnDied?.Invoke(attacker);
    }
}
