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

    }
}
