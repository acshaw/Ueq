using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class MobKillReward : NetworkBehaviour, IOnDeath
{
    MobApplicator _mob;

    // 5.3 (GP4) — same-zone party members within this distance of the mob's death position share credit.
    const float KillRangeRadius = 100f;

    void Awake() => _mob = GetComponent<MobApplicator>();

    public void OnDeath(NetworkIdentity attacker)
    {
        if (!isServer || attacker == null) return;

        var def = _mob?.Definition;
        if (def == null) return;

        // 5.3 (GP5) — resolve which group (a real party, or a solo player counted as a party of one) dealt
        // the majority of this mob's damage; that group gets both kill credit (XP, below) and exclusive
        // loot rights on the corpse. Reads EnemyAI's threat data, which its own OnDeath deliberately leaves
        // intact so this is correct regardless of IOnDeath dispatch order between components on this mob.
        var enemyAI  = GetComponent<EnemyAI>();
        var credited = enemyAI != null
            ? enemyAI.ResolveCreditedGroup(attacker)
            : new List<NetworkIdentity> { attacker };

        GetComponent<Corpse>()?.SetEligibleLooters(credited);

        if (def.xpReward > 0)
        {
            Vector3 deathPos = transform.position;
            var eligible = new List<NetworkIdentity>();
            foreach (var member in credited)
            {
                if (member == null) continue;
                if (member.gameObject.scene != gameObject.scene) continue; // GP4 — same zone as the mob
                if (Vector3.Distance(member.transform.position, deathPos) > KillRangeRadius) continue;
                eligible.Add(member);
            }

            if (eligible.Count > 0)
            {
                int share     = def.xpReward / eligible.Count;
                int remainder = def.xpReward - share * eligible.Count;
                // Remainder to the killing blow if they qualified; otherwise to any eligible member of the
                // credited group, rather than to an attacker outside the winning group or discarding it.
                NetworkIdentity remainderTo = eligible.Contains(attacker) ? attacker : eligible[0];

                foreach (var member in eligible)
                {
                    int amount = share + (member == remainderTo ? remainder : 0);
                    if (amount <= 0) continue;
                    member.GetComponent<PlayerExperience>()?.AddXp(amount);
                    var conn = member.connectionToClient;
                    if (conn != null)
                        ChatManager.Instance?.SendDirect(
                            new ChatMessage(ChatChannel.Reward, "System",
                                $"You gain {amount} experience from slaying {def.displayName}."),
                            conn);
                }
            }
        }

        // Faction hits stay killer-only (unchanged) — no clean case for splitting a standing change
        // across a party.
        if (def.factionHits != null && def.factionHits.Count > 0)
        {
            var scores = attacker.GetComponent<PlayerFactionScores>();
            if (scores != null)
            {
                var conn = attacker.connectionToClient;
                foreach (var hit in def.factionHits)
                {
                    if (hit.faction == null || hit.delta == 0) continue;
                    scores.ModifyScore(hit.faction, hit.delta);
                    if (conn != null)
                        ChatManager.Instance?.SendDirect(
                            new ChatMessage(ChatChannel.Reward, "System",
                                $"Your standing with {hit.faction.FactionName} has {(hit.delta > 0 ? "improved" : "worsened")}."),
                            conn);
                }
            }
        }
    }
}
