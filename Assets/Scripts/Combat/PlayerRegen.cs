using Mirror;

public class PlayerRegen : NetworkBehaviour
{
    Health        _health;
    PlayerMana    _mana;
    PlayerSitting _sitting;

    void Awake()
    {
        _health  = GetComponent<Health>();
        _mana    = GetComponent<PlayerMana>();
        _sitting = GetComponent<PlayerSitting>();
    }

    public override void OnStartServer()  => InvokeRepeating(nameof(Tick), 6f, 6f);
    public override void OnStopServer()   => CancelInvoke(nameof(Tick));

    [Server]
    void Tick()
    {
        if (_health.IsDead) return;

        // 3.1.7 — seated players recover twice as fast (the long-deferred "2/tick sitting" design).
        int amount = (_sitting != null && _sitting.IsSitting) ? 2 : 1;
        if (_health.Current < _health.Max) _health.Heal(amount);
        if (_mana != null && _mana.Current < _mana.Max) _mana.RestoreMana(amount);
    }
}
