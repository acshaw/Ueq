using Mirror;
using UnityEngine;

public class PlayerFactionScores : NetworkBehaviour
{
    [SerializeField] RaceDefaultsTable raceDefaults;   // headless/no-DB fallback only (M2.6 loads from DB)
    [SerializeField] string defaultRace = "Human";

    [SyncVar] public string ActualRace = "Human";
    [SyncVar] public string ApparentRace = "Human";

    // faction id (FactionDefinition.Key) → earned score; synced so clients can read standings for UI.
    // Re-keyed from faction name to faction id in M2.6 (DF2).
    readonly SyncDictionary<string, int> _scores = new();

    // Prefer DB-loaded race defaults (M2.6); fall back to the serialized SO when the registry is empty.
    RaceDefaultsTable EffectiveRaceDefaults => FactionRegistry.RaceDefaults ?? raceDefaults;

    // Fires on server when a score changes
    public event System.Action<FactionDefinition, int, int> OnScoreChanged; // (faction, old, new)

    public override void OnStartServer() => Initialize(defaultRace);

    [Server]
    public void Initialize(string race)
    {
        ActualRace   = race;
        ApparentRace = race;
        _scores.Clear(); // re-seeding (e.g. character creation picks a different race) starts clean
        var rd = EffectiveRaceDefaults;
        if (rd == null) return;
        foreach (var entry in rd.Defaults)
            if (entry.Race == race && entry.Faction != null)
                _scores[entry.Faction.Key] = entry.Score;
    }

    // Raw earned score regardless of illusion
    public int GetScore(FactionDefinition faction)
    {
        return _scores.TryGetValue(faction.Key, out int score) ? score : 0;
    }

    // Score used for NPC evaluation — substitutes race default when illusioned
    public int GetEffectiveScore(FactionDefinition faction)
    {
        var rd = EffectiveRaceDefaults;
        if (ApparentRace != ActualRace && rd != null)
            return rd.GetDefault(ApparentRace, faction);
        return GetScore(faction);
    }

    [Server]
    public void ModifyScore(FactionDefinition faction, int delta)
    {
        int old  = GetScore(faction);
        int next = old + delta;
        _scores[faction.Key] = next;
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
