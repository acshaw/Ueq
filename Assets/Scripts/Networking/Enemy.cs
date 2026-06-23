using Mirror;
using UnityEngine;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Targetable))]
[RequireComponent(typeof(NpcEventDispatcher))]
[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(NpcFaction))]
[RequireComponent(typeof(MobApplicator))]
[RequireComponent(typeof(Corpse))]
[RequireComponent(typeof(CombatLog))]
public class Enemy : NetworkBehaviour
{
    public override void OnStartServer()
    {
        var def = GetComponent<MobApplicator>()?.Definition;
        GetComponent<Nameplate>()?.SetLabel(def?.displayName ?? gameObject.name);
    }
}
