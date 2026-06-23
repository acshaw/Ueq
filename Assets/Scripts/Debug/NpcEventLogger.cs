using Mirror;
using UnityEngine;

// Attach to any NPC to log all NPC events to the console — test only.
public class NpcEventLogger : MonoBehaviour,
    IOnSpawned, IOnPerceived, IOnTargeted,
    IOnAttacked, IOnDeath, IOnAggroLost,
    IOnConversationStart, IOnConversationEnd, IOnConversationKeyword,
    IOnFactionChanged, IOnTimer
{
    public void OnSpawned()                                                => Log("Spawned");
    public void OnPerceived(NetworkIdentity player, float distance)        => Log($"Perceived {player.name} at {distance:F1}m");
    public void OnTargeted(NetworkIdentity player)                         => Log($"Targeted by {player.name}");
    public void OnAttacked(int damage, NetworkIdentity attacker)           => Log($"Attacked for {damage} by {(attacker ? attacker.name : "environment")}");
    public void OnDeath(NetworkIdentity attacker)                          => Log($"Died — attacker: {(attacker ? attacker.name : "environment")}");
    public void OnAggroLost()                                              => Log("Aggro lost");
    public void OnConversationStart(NetworkIdentity player)                => Log($"Conversation started with {player.name}");
    public void OnConversationEnd(NetworkIdentity player)                  => Log($"Conversation ended with {player.name}");
    public void OnConversationKeyword(NetworkIdentity player, string word) => Log($"Keyword '{word}' from {player.name}");
    public void OnFactionChanged(NetworkIdentity player, int old, int next) => Log($"Faction score for {player.name}: {old} → {next}");
    public void OnTimer()                                                  => Log("Timer tick");

    void Log(string msg) => Debug.Log($"[NpcEventLogger] {name}: {msg}");

    // Inspector button — works in Play mode on the server/host to test damage events.
    [ContextMenu("Test: Deal 10 Damage")]
    void TestDamage() => GetComponent<Health>()?.TakeDamage(10);
}
