using Mirror;
using UnityEngine;

public class PlayerMana : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnManaChanged))]
    int _current;

    [SyncVar(hook = nameof(OnMaxChanged))]
    int _maxSync;

    CharacterStats   _stats;
    PlayerExperience _exp;

    public int  Current           => _current;
    public int  Max               => isServer ? ComputeMax() : _maxSync;
    public bool HasMana(int cost) => _current >= cost;

    public event System.Action<int, int> OnManaUpdated;  // (current, max)

    void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _exp   = GetComponent<PlayerExperience>();
    }

    public override void OnStartServer()
    {
        _maxSync = ComputeMax();
        _current = _maxSync;
    }

    int ComputeMax()
    {
        var cls = _exp?.CurrentClass;
        if (cls == null || cls.manaStatType == ManaStatType.None) return 0;

        int level = _exp.Level;
        int stat  = cls.manaStatType == ManaStatType.Intellect ? _stats.Int : _stats.Wis;
        int effectiveStat = Mathf.Min(stat, cls.manaCap);
        float manaModifier = cls.baseManaRatio + (level - 1) * cls.manaGrowthRate;
        return cls.classBaseMana + (level - 1) * cls.manaPerLevel + Mathf.RoundToInt(effectiveStat * manaModifier);
    }

    [Server]
    public void RefreshMax()
    {
        int newMax = ComputeMax();
        _maxSync = newMax;
        if (_current > newMax) _current = newMax;
    }

    [Server]
    public bool UseMana(int amount)
    {
        if (_current < amount) return false;
        _current -= amount;
        return true;
    }

    [Server]
    public void RestoreMana(int amount)
    {
        int max = ComputeMax();
        if (_current >= max) return;
        _current = Mathf.Min(max, _current + amount);
    }

    /// <summary>Restore current mana from a loaded snapshot. Call AFTER <see cref="RefreshMax"/> so the
    /// value clamps against the correct max (1.3).</summary>
    [Server]
    public void SetCurrent(int value) => _current = Mathf.Clamp(value, 0, ComputeMax());

    void OnManaChanged(int _, int newVal) => OnManaUpdated?.Invoke(newVal, Max);
    void OnMaxChanged(int _, int __)      => OnManaUpdated?.Invoke(_current, Max);
}
