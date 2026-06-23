using Mirror;
using UnityEngine;

// Attach to any NPC. Implements IOnPerceived as the faction evaluation entry point.
public class NpcFaction : MonoBehaviour, IOnPerceived
{
    [SerializeField] FactionDefinition faction;

    [Header("Standing Boundaries")]
    [Tooltip("Standings at or below this index trigger aggro")]
    [SerializeField] string aggroMaxStanding = "Threatening";
    [Tooltip("Standings at or below this index (above aggro) trigger a warning")]
    [SerializeField] string warningMaxStanding = "Apprehensive";

    MobApplicator _mob;

    FactionDefinition EffectiveFaction         => _mob?.Definition?.faction          ?? faction;
    string            EffectiveAggroStanding    => _mob?.Definition?.aggroMaxStanding ?? aggroMaxStanding;
    string            EffectiveWarningStanding  => _mob?.Definition?.warningMaxStanding ?? warningMaxStanding;

    public FactionDefinition Faction => EffectiveFaction;

    void Awake() => _mob = GetComponent<MobApplicator>();

    public FactionThreshold EvaluatePlayer(NetworkIdentity player)
    {
        var f = EffectiveFaction;
        if (f == null || f.ThresholdTable == null) return default;
        var scores = player.GetComponent<PlayerFactionScores>();
        int score  = scores != null ? scores.GetEffectiveScore(f) : 0;
        return f.ThresholdTable.Evaluate(score);
    }

    // IOnPerceived — faction entry point; routes to aggro / warning / greet
    public void OnPerceived(NetworkIdentity player, float distance)
    {
        if (EffectiveFaction?.ThresholdTable == null) return;

        var standing    = EvaluatePlayer(player);
        var table       = EffectiveFaction.ThresholdTable;
        int idx         = table.IndexOf(standing.Name);
        int aggroMax    = table.IndexOf(EffectiveAggroStanding);
        int warningMax  = table.IndexOf(EffectiveWarningStanding);

        if (aggroMax >= 0 && idx <= aggroMax)
            OnAggroStanding(player, standing);
        else if (warningMax >= 0 && idx <= warningMax)
            OnWarningStanding(player, standing);
        else
            OnFriendlyStanding(player, standing);
    }

    void OnAggroStanding(NetworkIdentity player, FactionThreshold standing)
    {
        Debug.Log($"[Faction] {name} → {player.name} is {standing.Name} — aggro");
        var ai = GetComponent<EnemyAI>();
        if (ai != null) ai.AddThreat(player, ai.BaseAggroThreat);
    }

    void OnWarningStanding(NetworkIdentity player, FactionThreshold standing)
    {
        // TODO: trigger warning emote / con message
        Debug.Log($"[Faction] {name} → {player.name} is {standing.Name} — would warn");
    }

    void OnFriendlyStanding(NetworkIdentity player, FactionThreshold standing)
    {
        // TODO: trigger greeting via conversation system
        Debug.Log($"[Faction] {name} → {player.name} is {standing.Name} — would greet");
    }
}
