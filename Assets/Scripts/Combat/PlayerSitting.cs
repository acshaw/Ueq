using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Toggleable seated state (3.1.7) — EQ-style rest. Server-authoritative <see cref="SyncVar"/>; toggled via
/// the <c>/sit</c> chat command or hotbar key <c>0</c>. Taking damage (<see cref="Health.TakeDamage"/>) or
/// moving (<c>NetworkedPlayer.CmdSendInput</c>) stands the player, server-side. The synced flag drives a
/// "Sitting" bool on the body's Animator so the seated pose shows for local + remote players.
///
/// Consumers: <see cref="PlayerRegen"/> doubles regen while seated (the long-deferred 2/tick design), and the
/// 3.1.8 camp gate refuses <c>/camp</c> unless <see cref="IsSitting"/>. Mirrors the <see cref="CombatState"/>
/// component pattern.
/// </summary>
public class PlayerSitting : NetworkBehaviour
{
    [SyncVar] bool _sitting;

    public bool IsSitting => _sitting;

    static readonly int SittingHash = Animator.StringToHash("Sitting");
    Animator _animator;
    bool     _hasSittingParam;

    // ── Toggle / set (client → server) ───────────────────────────────────────────

    [Command] public void CmdToggleSit() => ServerSetSit(!_sitting);
    [Command] public void CmdStand()     => ServerSetSit(false);

    [Server]
    public void ServerSetSit(bool sitting)
    {
        if (_sitting != sitting) _sitting = sitting;
    }

    /// <summary>Server-side stand — called from the damage + movement hooks.</summary>
    [Server]
    public void ServerStand() => ServerSetSit(false);

    // ── Input + animation ────────────────────────────────────────────────────────

    void Update()
    {
        // Local player: poll the sit hotkey (suppressed while chat is open, like the other hotkeys).
        if (isLocalPlayer && !ChatUI.IsOpen)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.digit0Key.wasPressedThisFrame)
                CmdToggleSit();
        }

        // Every client (incl. host): drive the Animator's Sitting bool from the synced flag. Re-resolve the
        // Animator when it's gone — PlayerModel rebuilds the body child on a race/class change (3.1.4/3.1.6).
        if (!NetworkClient.active) return;
        if (_animator == null) ResolveAnimator();
        if (_animator != null && _hasSittingParam) _animator.SetBool(SittingHash, _sitting);
    }

    void ResolveAnimator()
    {
        _animator = GetComponentInChildren<Animator>();
        _hasSittingParam = false;
        if (_animator == null || _animator.runtimeAnimatorController == null) return;
        foreach (var p in _animator.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == "Sitting") { _hasSittingParam = true; break; }
    }
}
