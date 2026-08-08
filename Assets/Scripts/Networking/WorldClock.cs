using Mirror;
using UnityEngine;

/// <summary>
/// 5.12 (DC1) — server-authoritative day/night + lunar clock. Deliberately NOT a per-frame SyncVar: the
/// server sends one small message carrying its NetworkTime-anchored start reference on
/// <see cref="GameNetworkManager.OnServerReady"/> (same shape as <c>ContentCatalog</c>'s content push),
/// and every peer (server, host, and every remote client) computes <see cref="DayFraction"/> /
/// <see cref="LunarFraction"/> locally from Mirror's shared <c>NetworkTime.time</c>. Zero ongoing sync
/// traffic, always in lockstep, and a late-joining client shows the correct current time immediately
/// instead of resetting to dawn.
///
/// Lives on the NetworkManager GameObject (wired by <c>Tools/World Clock/Setup Scene</c>, mirroring how
/// <c>ZoneManager</c> is wired by <c>Tools/Zones/Build Zone Scenes</c>) and is driven by
/// <see cref="GameNetworkManager"/>'s ServerInitialize/ServerShutdown calls.
/// </summary>
public class WorldClock : MonoBehaviour
{
    public static WorldClock Instance { get; private set; }

    static double _startNetworkTime;
    static float  _dayLengthSeconds  = 3000f;  // overwritten by WorldClockSettings/sync — 50 min default
    static float  _lunarCycleSeconds = 24000f; // 8 in-game days default

    // Distance fog (post-8.8-session follow-up) — owned here rather than by SkyDriver locally, same
    // reasoning as day length/lunar cycle: needs to be DB-authored and identical on every peer, so it
    // rides the same resolve-once-on-server + sync-to-client path instead of being a client-local
    // Inspector-only tunable. Defaults match SkyDriver's original hardcoded fallback.
    static float _fogStartDistance = 120f;
    static float _fogEndDistance   = 520f;

    // Tools/World Clock Debug — scrub overrides. -1 disables (falls back to the real computed fraction).
    static float _debugDayOverride   = -1f;
    static float _debugLunarOverride = -1f;

    // 5.12 follow-up — DB-authored override (world_clock_settings row), highest priority ahead of the
    // WorldClockSettings Resources asset / in-memory defaults (fog has no asset tier — DB or hardcoded
    // default only). Set by ContentLoader before ServerInitialize reads it; null means "no row yet, use
    // the existing fallback chain unchanged."
    static float? _dbDayLengthMinutes;
    static float? _dbLunarCycleDays;
    static float? _dbFogStartDistance;
    static float? _dbFogEndDistance;

    /// <summary>Server-only: called by ContentLoader with the DB row's values, if one exists.</summary>
    public static void SetDbSettingsOverride(
        float? dayLengthMinutes, float? lunarCycleDays, float? fogStartDistance, float? fogEndDistance)
    {
        _dbDayLengthMinutes  = dayLengthMinutes;
        _dbLunarCycleDays    = lunarCycleDays;
        _dbFogStartDistance  = fogStartDistance;
        _dbFogEndDistance    = fogEndDistance;
    }

    /// <summary>Server → client one-time clock reference (DC1). No per-frame traffic.</summary>
    public struct WorldClockSyncMessage : NetworkMessage
    {
        public double startNetworkTime;
        public float  dayLengthSeconds;
        public float  lunarCycleSeconds;
        public float  fogStartDistance;
        public float  fogEndDistance;
    }

    // ── Server lifecycle (mirrors ZoneManager.ServerInitialize/ServerShutdown) ──────────────────────────

    public void ServerInitialize()
    {
        Instance = this;

        // DB override (if ContentLoader found a world_clock_settings row) takes priority over the
        // Resources asset/in-memory defaults. Resolved through a throwaway WorldClockSettings instance
        // so DayLengthSeconds/LunarCycleSeconds's clamping formula isn't duplicated here.
        var baseline = WorldClockSettings.Active;
        var effective = ScriptableObject.CreateInstance<WorldClockSettings>();
        effective.dayLengthMinutes = _dbDayLengthMinutes ?? baseline.dayLengthMinutes;
        effective.lunarCycleDays   = _dbLunarCycleDays   ?? baseline.lunarCycleDays;

        _startNetworkTime  = NetworkTime.time;
        _dayLengthSeconds  = effective.DayLengthSeconds;
        _lunarCycleSeconds = effective.LunarCycleSeconds;
        _fogStartDistance  = _dbFogStartDistance ?? _fogStartDistance;
        _fogEndDistance    = _dbFogEndDistance   ?? _fogEndDistance;

        bool dbOverride = _dbDayLengthMinutes.HasValue || _dbLunarCycleDays.HasValue
            || _dbFogStartDistance.HasValue || _dbFogEndDistance.HasValue;
        Debug.Log($"[WorldClock] Server clock started. Day length {_dayLengthSeconds:F0}s, " +
                   $"lunar cycle {_lunarCycleSeconds:F0}s, fog {_fogStartDistance:F0}-{_fogEndDistance:F0}u" +
                   $"{(dbOverride ? " (DB override)" : "")}.");
    }

    public void ServerShutdown()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Server: the sync message to push to a client on ready.</summary>
    public static WorldClockSyncMessage BuildSync() => new WorldClockSyncMessage
    {
        startNetworkTime  = _startNetworkTime,
        dayLengthSeconds  = _dayLengthSeconds,
        lunarCycleSeconds = _lunarCycleSeconds,
        fogStartDistance  = _fogStartDistance,
        fogEndDistance    = _fogEndDistance,
    };

    /// <summary>Client: apply the server's clock reference. No-op on host — the server already set the
    /// statics directly via ServerInitialize, and host shares that same process/state.</summary>
    public static void ApplySync(WorldClockSyncMessage msg)
    {
        if (NetworkServer.active) return;
        _startNetworkTime  = msg.startNetworkTime;
        _dayLengthSeconds  = msg.dayLengthSeconds;
        _lunarCycleSeconds = msg.lunarCycleSeconds;
        _fogStartDistance  = msg.fogStartDistance;
        _fogEndDistance    = msg.fogEndDistance;
    }

    // ── Read API — every peer computes identically from NetworkTime ────────────────────────────────────

    /// <summary>0..1 fraction through the current in-game day. 0/1 = midnight, 0.5 = noon.</summary>
    public static float DayFraction
    {
        get
        {
            if (_debugDayOverride >= 0f) return _debugDayOverride;
            if (_dayLengthSeconds <= 0f) return 0f;
            double t = NetworkTime.time - _startNetworkTime;
            double f = (t % _dayLengthSeconds) / _dayLengthSeconds;
            if (f < 0) f += 1.0;
            return (float)f;
        }
    }

    /// <summary>0..1 fraction through the current lunar cycle. 0/1 = new moon, 0.5 = full moon.</summary>
    public static float LunarFraction
    {
        get
        {
            if (_debugLunarOverride >= 0f) return _debugLunarOverride;
            if (_lunarCycleSeconds <= 0f) return 0f;
            double t = NetworkTime.time - _startNetworkTime;
            double f = (t % _lunarCycleSeconds) / _lunarCycleSeconds;
            if (f < 0) f += 1.0;
            return (float)f;
        }
    }

    /// <summary>DC6 — cosmetic-only lunar hook. Public read of the current phase for a future content
    /// item (e.g. a 7.x spawn table) to opt into a full-moon condition later, without this devplan
    /// reaching into SpawnTable/SpawnPoint at all.</summary>
    public static LunarPhase CurrentLunarPhase => LunarPhaseUtil.FromFraction(LunarFraction);

    /// <summary>Distance fog tunables (post-8.8-session follow-up) — read by SkyDriver every frame rather
    /// than owning its own local fields, so a DB-authored value applies identically on every peer via the
    /// same resolve-once-on-server + sync-to-client path as day length/lunar cycle.</summary>
    public static float FogStartDistance => _fogStartDistance;
    public static float FogEndDistance   => _fogEndDistance;

    // ── Debug override (Tools/World Clock Debug) ────────────────────────────────────────────────────────

    public static bool  DebugOverrideActive => _debugDayOverride >= 0f;
    public static void  SetDebugDayFraction(float f)   => _debugDayOverride = Mathf.Repeat(f, 1f);
    public static void  SetDebugLunarFraction(float f) => _debugLunarOverride = Mathf.Repeat(f, 1f);
    public static void  ClearDebugOverrides() { _debugDayOverride = -1f; _debugLunarOverride = -1f; }
}

/// <summary>DC6 — the 8 traditional lunar phases, indexed from <see cref="WorldClock.LunarFraction"/>.</summary>
public enum LunarPhase
{
    New, WaxingCrescent, FirstQuarter, WaxingGibbous,
    Full, WaningGibbous, LastQuarter, WaningCrescent,
}

public static class LunarPhaseUtil
{
    public static LunarPhase FromFraction(float f)
    {
        int idx = Mathf.FloorToInt(Mathf.Repeat(f, 1f) * 8f) % 8;
        return (LunarPhase)idx;
    }
}
