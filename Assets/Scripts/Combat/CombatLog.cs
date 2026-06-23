using Mirror;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class CombatLog : NetworkBehaviour
{
    Health _health;

    void Awake() => _health = GetComponent<Health>();

    public override void OnStartServer()
    {
        _health.OnDamaged -= OnDamaged;
        _health.OnDied    -= OnDied;
        _health.OnDamaged += OnDamaged;
        _health.OnDied    += OnDied;
    }

    public override void OnStopServer()
    {
        _health.OnDamaged -= OnDamaged;
        _health.OnDied    -= OnDied;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    void OnDamaged(int amount, NetworkIdentity attacker)
    {
        string targetName   = Name(netIdentity);
        string attackerName = Name(attacker);

        // "You hit Giant Rat for 5 damage."
        Send(attacker, $"You hit {targetName} for {amount} damage.");

        // "Giant Rat hits you for 5 damage."
        Send(netIdentity, $"{attackerName} hits you for {amount} damage.");
    }

    void OnDied(NetworkIdentity attacker)
    {
        string targetName   = Name(netIdentity);
        string attackerName = Name(attacker);

        // "You have slain Giant Rat!"
        Send(attacker, $"You have slain {targetName}!");

        // "You have been slain by Giant Rat!"
        Send(netIdentity, $"You have been slain by {attackerName}!");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Sends only if the recipient is a player connection; silently no-ops for NPCs.
    static void Send(NetworkIdentity recipient, string text)
    {
        var conn = recipient?.connectionToClient;
        if (conn == null) return;
        ChatManager.Instance?.SendDirect(
            new ChatMessage(ChatChannel.Combat, "", text), conn);
    }

    static string Name(NetworkIdentity ni)
    {
        if (ni == null) return "Unknown";
        return ni.GetComponent<Nameplate>()?.Label ?? ni.gameObject.name;
    }
}
