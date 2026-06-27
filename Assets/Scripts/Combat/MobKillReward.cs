using Mirror;
using UnityEngine;

public class MobKillReward : NetworkBehaviour, IOnDeath
{
    MobApplicator _mob;

    void Awake() => _mob = GetComponent<MobApplicator>();

    public void OnDeath(NetworkIdentity attacker)
    {
        if (!isServer || attacker == null) return;

        var def  = _mob?.Definition;
        if (def == null) return;

        var conn = attacker.connectionToClient;
        if (conn == null) return;

        if (def.xpReward > 0)
        {
            attacker.GetComponent<PlayerExperience>()?.AddXp(def.xpReward);
            ChatManager.Instance?.SendDirect(
                new ChatMessage(ChatChannel.Reward, "System",
                    $"You gain {def.xpReward} experience from slaying {def.displayName}."),
                conn);
        }

        // M2.7.1: apply faction hits to the killer (killer-only for now, like XP).
        if (def.factionHits != null && def.factionHits.Count > 0)
        {
            var scores = attacker.GetComponent<PlayerFactionScores>();
            if (scores != null)
            {
                foreach (var hit in def.factionHits)
                {
                    if (hit.faction == null || hit.delta == 0) continue;
                    scores.ModifyScore(hit.faction, hit.delta);
                    ChatManager.Instance?.SendDirect(
                        new ChatMessage(ChatChannel.Reward, "System",
                            $"Your standing with {hit.faction.FactionName} has {(hit.delta > 0 ? "improved" : "worsened")}."),
                        conn);
                }
            }
        }
    }
}
