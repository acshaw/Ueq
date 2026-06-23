using System;
using Mirror;
using UnityEngine;

/// <summary>
/// Single source of truth for "the local player" (1.7). Panels subscribe to <see cref="Spawned"/> /
/// <see cref="Despawned"/> instead of each polling <c>FindObjectsByType&lt;NetworkedPlayer&gt;()</c> —
/// which is what made HUD binding fragile across host-restart / camp (the 1.3 rebind bug). Fed by the
/// player itself via <see cref="NetworkedPlayer"/> (OnStartLocalPlayer → <see cref="Set"/>,
/// OnStopClient → <see cref="Clear"/>).
///
/// Late-subscriber pattern: a panel that enables after the player already spawned should, right after
/// subscribing, check <see cref="Current"/> and bind immediately if it's non-null (so it doesn't miss
/// the one-shot <see cref="Spawned"/> event).
/// </summary>
public static class LocalPlayer
{
    public static NetworkedPlayer Current { get; private set; }

    /// <summary>Fired when the local player enters the world (spawn / character enter).</summary>
    public static event Action<NetworkedPlayer> Spawned;

    /// <summary>Fired when the local player leaves (camp / disconnect / host restart).</summary>
    public static event Action Despawned;

    [RuntimeInitializeOnLoadMethod] // reset statics for fast play-mode (no domain reload)
    static void ResetStatics()
    {
        Current   = null;
        Spawned   = null;
        Despawned = null;
    }

    public static void Set(NetworkedPlayer player)
    {
        if (player == null) return;
        Current = player;
        Spawned?.Invoke(player);
    }

    public static void Clear()
    {
        if (Current == null) return;
        Current = null;
        Despawned?.Invoke();
    }
}
