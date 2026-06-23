using Mirror;

public class PlayerRegen : NetworkBehaviour
{
    Health     _health;
    PlayerMana _mana;

    void Awake()
    {
        _health = GetComponent<Health>();
        _mana   = GetComponent<PlayerMana>();
    }

    public override void OnStartServer()  => InvokeRepeating(nameof(Tick), 6f, 6f);
    public override void OnStopServer()   => CancelInvoke(nameof(Tick));

    [Server]
    void Tick()
    {
        if (_health.IsDead) return;
        if (_health.Current < _health.Max) _health.Heal(1);
        if (_mana != null && _mana.Current < _mana.Max) _mana.RestoreMana(1);
    }
}
