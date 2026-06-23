using Mirror;
using UnityEngine;

public class PlayerExperience : NetworkBehaviour
{
    [SerializeField] RaceDefinition  _defaultRace;
    [SerializeField] ClassDefinition _defaultClass;

    [SyncVar(hook = nameof(OnXpChanged))] int   _totalXp;
    [SyncVar]                             float _xpModifier = 1f;

    RaceDefinition  _currentRace;
    ClassDefinition _currentClass;

    public int             TotalXp      => _totalXp;
    public float           Modifier     => _xpModifier;
    public int             Level        => ComputeLevel(_totalXp, _xpModifier);
    public ClassDefinition CurrentClass => _currentClass;
    public RaceDefinition  CurrentRace  => _currentRace;

    // ── XP table (loaded from Resources/XpTable.asset) ───────────────────────

    static XpTableDefinition _tableCache;
    static XpTableDefinition Table
        => _tableCache != null ? _tableCache
                               : (_tableCache = Resources.Load<XpTableDefinition>("XpTable"));

    public static int MaxLevel => Table != null ? Table.Count : XpTableDefinition.DefaultValues.Length;

    public static int XpForLevel(int level)
    {
        var t = Table;
        if (t != null) return t.XpForLevel(level);
        if (level < 1 || level > XpTableDefinition.DefaultValues.Length) return 0;
        return XpTableDefinition.DefaultValues[level - 1];
    }

    public static int TotalXpToReachLevel(int level, float modifier = 1f)
    {
        var t = Table;
        if (t != null) return t.TotalXpToReach(level, modifier);
        if (level <= 1) return 0;
        int sum = 0;
        var def = XpTableDefinition.DefaultValues;
        for (int i = 1; i < level && i <= def.Length; i++)
            sum += Mathf.RoundToInt(def[i - 1] * modifier);
        return sum;
    }

    public static int ComputeLevel(int totalXp, float modifier = 1f)
    {
        int max   = MaxLevel;
        int level = 1;
        while (level < max && totalXp >= TotalXpToReachLevel(level + 1, modifier))
            level++;
        return level;
    }

    public int DeathXpLoss() => Mathf.RoundToInt(XpForLevel(Level) * _xpModifier * 0.1f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void OnStartServer()
    {
        if (_defaultRace != null || _defaultClass != null)
            ApplyModifier(_defaultRace, _defaultClass);
    }

    // ── Race / class ──────────────────────────────────────────────────────────

    [Server]
    public void SetRaceClass(RaceDefinition race, ClassDefinition cls) => ApplyModifier(race, cls);

    // ── Persistence (1.3) ───────────────────────────────────────────────────────

    /// <summary>Restore persisted XP + race/class, then recompute everything derived
    /// (stats, HP/mana max, known abilities + default hotbar) via the normal apply path.</summary>
    [Server]
    public void LoadState(int totalXp, RaceDefinition race, ClassDefinition cls)
    {
        _totalXp = Mathf.Max(0, totalXp);
        ApplyModifier(race, cls);
    }

    void ApplyModifier(RaceDefinition race, ClassDefinition cls)
    {
        _currentRace  = race;
        _currentClass = cls;
        float r = race != null ? race.xpModifier : 1f;
        float c = cls  != null ? cls.xpModifier  : 1f;
        _xpModifier = r * c;
        GetComponent<CharacterStats>()?.SetRaceClass(race, cls);
        GetComponent<Health>()?.RefreshMax();
        GetComponent<PlayerMana>()?.RefreshMax();
        GetComponent<PlayerAbilities>()?.SetRaceClass(cls);
    }

    // ── XP mutation ───────────────────────────────────────────────────────────

    [Server]
    public void AddXp(int amount)
    {
        if (amount <= 0) return;
        int before = Level;
        _totalXp += amount;
        if (Level > before)
        {
            ChatManager.Instance?.SendDirect(
                new ChatMessage(ChatChannel.Reward, "System", $"You have reached level {Level}!"),
                connectionToClient);
            GetComponent<Health>()?.RefreshMax();
            GetComponent<PlayerMana>()?.RefreshMax();
        }
    }

    [Server]
    public void RemoveXp(int amount)
    {
        if (amount <= 0) return;
        int before = Level;
        _totalXp = Mathf.Max(0, _totalXp - amount);
        if (Level < before)
        {
            ChatManager.Instance?.SendDirect(
                new ChatMessage(ChatChannel.System, "System", $"You have lost a level! You are now level {Level}."),
                connectionToClient);
            GetComponent<Health>()?.RefreshMax();
            GetComponent<PlayerMana>()?.RefreshMax();
        }
    }

    void OnXpChanged(int _, int __) { /* hook for XP bar UI */ }
}
