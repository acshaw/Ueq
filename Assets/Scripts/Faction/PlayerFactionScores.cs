using Mirror;
using UnityEngine;

public class PlayerFactionScores : NetworkBehaviour
{
    [SerializeField] RaceDefaultsTable raceDefaults;
    [SerializeField] string defaultRace = "Human";

    [SyncVar] public string ActualRace = "Human";
    [SyncVar] public string ApparentRace = "Human";

    // faction name → earned score; synced so clients can read standings for UI
    readonly SyncDictionary<string, int> _scores = new();

    // Fires on server when a score changes
    public event System.Action<FactionDefinition, int, int> OnScoreChanged; // (faction, old, new)

    public override void OnStartServer() => Initialize(defaultRace);

    [Server]
    public void Initialize(string race)
    {
        ActualRace   = race;
        ApparentRace = race;
        _scores.Clear(); // re-seeding (e.g. character creation picks a different race) starts clean
        if (raceDefaults == null) return;
        foreach (var entry in raceDefaults.Defaults)
            if (entry.Race == race)
                _scores[entry.Faction.FactionName] = entry.Score;
    }

    // Raw earned score regardless of illusion
    public int GetScore(FactionDefinition faction)
    {
        return _scores.TryGetValue(faction.FactionName, out int score) ? score : 0;
    }

    // Score used for NPC evaluation — substitutes race default when illusioned
    public int GetEffectiveScore(FactionDefinition faction)
    {
        if (ApparentRace != ActualRace && raceDefaults != null)
            return raceDefaults.GetDefault(ApparentRace, faction);
        return GetScore(faction);
    }

    [Server]
    public void ModifyScore(FactionDefinition faction, int delta)
    {
        int old  = GetScore(faction);
        int next = old + delta;
        _scores[faction.FactionName] = next;
        OnScoreChanged?.Invoke(faction, old, next);
    }

    [Server] public void SetIllusion(string apparentRace) => ApparentRace = apparentRace;
    [Server] public void ClearIllusion()                  => ApparentRace = ActualRace;

    // ── Persistence (1.3) ───────────────────────────────────────────────────────

    /// <summary>Restore earned scores + actual/apparent race from a loaded snapshot.</summary>
    [Server]
    public void LoadState(string actualRace, string apparentRace, System.Collections.Generic.Dictionary<string, int> scores)
    {
        ActualRace   = string.IsNullOrEmpty(actualRace)   ? defaultRace : actualRace;
        ApparentRace = string.IsNullOrEmpty(apparentRace) ? ActualRace  : apparentRace;
        _scores.Clear();
        if (scores != null)
            foreach (var kv in scores)
                _scores[kv.Key] = kv.Value;
    }

    /// <summary>Export earned scores as a plain dictionary for a snapshot.</summary>
    public System.Collections.Generic.Dictionary<string, int> ExportScores()
    {
        var d = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var kv in _scores)
            d[kv.Key] = kv.Value;
        return d;
    }
}
