using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 5.4 (AG1) — the "consider" mechanic: press C to learn a targeted mob/NPC's faction disposition on
/// demand (right-click on a live target does the same, wired in NetworkedPlayer's RMB handler). Mirrors
/// PlayerSitting's pattern — a small dedicated component owning its own [Command]. Purely player-triggered,
/// no automatic ping.
/// </summary>
public class PlayerConsider : NetworkBehaviour
{
    NetworkedPlayer _player;
    void Awake() => _player = GetComponent<NetworkedPlayer>();

    void Update()
    {
        if (!isLocalPlayer || ChatUI.IsOpen) return;
        var kb = Keyboard.current;
        if (kb != null && kb.cKey.wasPressedThisFrame)
            CmdConsider(_player.CurrentTargetIdentity);
    }

    [Command]
    public void CmdConsider(NetworkIdentity target)
    {
        if (target == null)
        {
            ChatManager.Instance?.SendDirect(
                new ChatMessage(ChatChannel.Consider, "System", "You have no target to consider."),
                connectionToClient);
            return;
        }

        var faction = target.GetComponent<NpcFaction>();
        if (faction == null) return; // not a faction-bearing NPC — silently no-op, matches OnPerceived's own gate

        var standing = faction.EvaluatePlayer(netIdentity);
        string label = target.GetComponent<Nameplate>()?.Label ?? target.gameObject.name;

        string text = string.IsNullOrEmpty(standing.ConsiderText)
            ? $"{label} regards you."
            : $"{label} {standing.ConsiderText}.";

        ChatManager.Instance?.SendDirect(
            new ChatMessage(ChatChannel.Consider, "System", text), connectionToClient);
    }
}
