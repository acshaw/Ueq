using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 5.4 (AG1) — the "consider" mechanic: press C to learn a targeted mob/NPC's faction disposition on
/// demand (right-click on a live target does the same, wired in NetworkedPlayer's RMB handler). Mirrors
/// PlayerSitting's pattern — a small dedicated component owning its own [Command]. Purely player-triggered,
/// no automatic ping.
///
/// 8.3 (LV2a) — extended with an EQ1-style level-banded continuation: the faction sentence is followed by
/// a pronoun continuation ("It ...") colored/worded by how the target's level compares to the player's,
/// using the same Fibonacci-band math (CombatResolver.LevelGapFraction) that drives the actual hit-roll
/// differential, so what /con tells you stays consistent with what a real fight would look like.
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

        string factionText = string.IsNullOrEmpty(standing.ConsiderText)
            ? $"{label} regards you."
            : $"{label} {standing.ConsiderText}.";

        string levelText = BuildLevelConsiderText(target);
        string text = string.IsNullOrEmpty(levelText) ? factionText : $"{factionText} {levelText}";

        ChatManager.Instance?.SendDirect(
            new ChatMessage(ChatChannel.Consider, "System", text), connectionToClient);
    }

    // 8.3 (LV2a) — a pronoun continuation ("It ..."), not a repeated {label}, so it reads naturally after
    // any of the faction phrases above regardless of which one preceded it. Null if the target has no
    // resolvable level (shouldn't happen for an NpcFaction-bearing target — every mob/NPC shares the same
    // Enemy prefab with MobApplicator — but guarded rather than assumed).
    string BuildLevelConsiderText(NetworkIdentity target)
    {
        var mobApp = target.GetComponent<MobApplicator>();
        if (mobApp == null || mobApp.Definition == null) return null;
        int targetLevel = mobApp.LevelOverride ?? mobApp.Definition.mobLevel;

        int playerLevel = GetComponent<PlayerExperience>()?.Level ?? 1;
        int gap = playerLevel - targetLevel;
        float fraction = CombatResolver.LevelGapFraction(playerLevel, targetLevel);

        var (hex, phrase) = LevelBand(gap, fraction);
        return $"<color=#{hex}>It {phrase}</color>";
    }

    // 8.3 (LV2a) — 6 tiers, asymmetric by design: three gradations on the favors-you side (a genuine
    // "risky but winnable" tier between even and comfortable), two on the favors-mob side.
    static (string hex, string phrase) LevelBand(int gap, float fraction)
    {
        if (gap == 0) return ("FFFFFF", "looks like an even match for you.");

        if (gap < 0) // mob is the higher level — favors the mob
            return fraction >= 0.5f
                ? ("FF4D4D", "would be a great danger to you.")
                : ("FFD24D", "could be dangerous.");

        // gap > 0 — mob is the lower level — favors the player
        if (fraction >= 0.5f)  return ("33CC33", "would be a trivial kill for you.");
        if (fraction >= 0.25f) return ("4DA6FF", "shouldn't be much of a challenge for you.");
        return ("99D6FF", "looks like a difficult fight, but you should prevail.");
    }
}
