using Mirror;
using Newtonsoft.Json;
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
///
/// 8.1 extends this from a cosmetic, session-relative day/night+lunar cycle into a persistent,
/// server-authoritative calendar (Age → Year → Month → Week → Day) — see the "Calendar (8.1)" region
/// below. <c>_startNetworkTime</c> itself is now backed by a DB-persisted elapsed-seconds counter
/// (8.1.1) so it survives a server restart instead of resetting to zero every boot.
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

    // ── Calendar (8.1) — persisted world state, distinct from the tunables above ───────────────────────

    // 8.1.1 (CAL1): accumulated simulated seconds since the world's Day 1, as of the last load/save —
    // NOT a tunable with a Resources-asset fallback tier, so it's set via a separate method
    // (SetPersistedCalendarState), not folded into SetDbSettingsOverride above.
    static double _calendarElapsedAtLoad;

    // 8.1.3 (CAL5-CAL7): age is manually set, never derived from elapsed time. _ageStartedElapsedDays is
    // the WorldElapsedDays value the current age began on — every calendar-display unit derives from
    // AgeElapsedDays (= WorldElapsedDays - _ageStartedElapsedDays), not WorldElapsedDays directly, so a
    // new age resets Day/Week/Month/Year cleanly (CAL6a) while DayFraction (time-of-day) — which never
    // references this field — stays completely untouched.
    static int  _age                    = 1;
    static long _ageStartedElapsedDays  = 0;
    static int  _ageStartingYearDisplay = 1372;

    // 8.1.6 (CAL8): renameable calendar display names. Index 0 = day/month 1. Always length 7 / 13.
    static string[] _dayNames   = DefaultDayNames();
    static string[] _monthNames = DefaultMonthNames();

    static string[] DefaultDayNames() => new[] { "Day 1", "Day 2", "Day 3", "Day 4", "Day 5", "Day 6", "Day 7" };
    static string[] DefaultMonthNames() => new[]
    {
        "Month 1", "Month 2", "Month 3", "Month 4", "Month 5", "Month 6",
        "Month 7", "Month 8", "Month 9", "Month 10", "Month 11", "Month 12", "Month 13",
    };

    /// <summary>Server-only: called by ContentLoader with the persisted calendar row, if one exists.
    /// Null values (a brand-new world, no row yet) fall back to the class field defaults above (Age 1,
    /// Day 1, "Year 1372").</summary>
    public static void SetPersistedCalendarState(
        double? calendarElapsedSeconds, int? age, long? ageStartedElapsedDays, int? ageStartingYearDisplay)
    {
        _calendarElapsedAtLoad   = calendarElapsedSeconds ?? 0;
        _age                     = age ?? 1;
        _ageStartedElapsedDays   = ageStartedElapsedDays ?? 0;
        _ageStartingYearDisplay  = ageStartingYearDisplay ?? 1372;
    }

    /// <summary>Server-only: called by ContentLoader with the day/month name rows. Falls back to the
    /// numbered placeholders above if a table is empty/missing (e.g. migration not yet applied) — and
    /// per-slot, if an individual row is missing (a gap, not the whole table), so a partially-seeded
    /// table never produces a blank/null display name.</summary>
    public static void SetDayMonthNames(string[] dayNames, string[] monthNames)
    {
        if (dayNames != null && dayNames.Length == DaysPerWeek)
            for (int i = 0; i < dayNames.Length; i++)
                if (!string.IsNullOrEmpty(dayNames[i])) _dayNames[i] = dayNames[i];
        if (monthNames != null && monthNames.Length == MonthsPerYear)
            for (int i = 0; i < monthNames.Length; i++)
                if (!string.IsNullOrEmpty(monthNames[i])) _monthNames[i] = monthNames[i];
    }

    /// <summary>Server → client one-time clock reference (DC1). No per-frame traffic. 8.1 extends this
    /// with the age anchor (CAL3 correction — every calendar-display unit is age-relative, not just Age
    /// itself, so ageStartedElapsedDays/ageStartingYearDisplay must sync too) and the day/month display
    /// names, JSON-encoded the same way ContentCatalog's richer payloads already are.</summary>
    public struct WorldClockSyncMessage : NetworkMessage
    {
        public double startNetworkTime;
        public float  dayLengthSeconds;
        public float  lunarCycleSeconds;
        public float  fogStartDistance;
        public float  fogEndDistance;
        public int    age;
        public long   ageStartedElapsedDays;
        public int    ageStartingYearDisplay;
        public string dayMonthNamesJson; // {"d":[...7],"m":[...13]} — see DayMonthNames struct below
    }

    struct DayMonthNames
    {
        public string[] d;
        public string[] m;
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

        _dayLengthSeconds  = effective.DayLengthSeconds;
        _lunarCycleSeconds = effective.LunarCycleSeconds;
        _fogStartDistance  = _dbFogStartDistance ?? _fogStartDistance;
        _fogEndDistance    = _dbFogEndDistance   ?? _fogEndDistance;

        // 8.1.1 (CAL1): resume from the persisted elapsed-seconds counter instead of always starting at
        // zero — this is the actual fix for the "every restart resets the calendar" gap. Same arithmetic
        // ServerSetHour already used to shift the epoch, just seeded from DB state instead of discarded.
        _startNetworkTime = NetworkTime.time - _calendarElapsedAtLoad;

        bool dbOverride = _dbDayLengthMinutes.HasValue || _dbLunarCycleDays.HasValue
            || _dbFogStartDistance.HasValue || _dbFogEndDistance.HasValue;
        Debug.Log($"[WorldClock] Server clock started. Day length {_dayLengthSeconds:F0}s, " +
                   $"lunar cycle {_lunarCycleSeconds:F0}s, fog {_fogStartDistance:F0}-{_fogEndDistance:F0}u" +
                   $"{(dbOverride ? " (DB override)" : "")}. Calendar resumed at {_calendarElapsedAtLoad:F0}s " +
                   $"elapsed, Age {_age}, Year {Year}, {DayOfWeekName} (Day {DayOfMonth} of {MonthName}).");
    }

    public void ServerShutdown()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Server: the sync message to push to a client on ready.</summary>
    public static WorldClockSyncMessage BuildSync() => new WorldClockSyncMessage
    {
        startNetworkTime       = _startNetworkTime,
        dayLengthSeconds       = _dayLengthSeconds,
        lunarCycleSeconds      = _lunarCycleSeconds,
        fogStartDistance       = _fogStartDistance,
        fogEndDistance         = _fogEndDistance,
        age                    = _age,
        ageStartedElapsedDays  = _ageStartedElapsedDays,
        ageStartingYearDisplay = _ageStartingYearDisplay,
        dayMonthNamesJson      = JsonConvert.SerializeObject(new DayMonthNames { d = _dayNames, m = _monthNames }),
    };

    /// <summary>Client: apply the server's clock reference. No-op on host — the server already set the
    /// statics directly via ServerInitialize, and host shares that same process/state.</summary>
    public static void ApplySync(WorldClockSyncMessage msg)
    {
        if (NetworkServer.active) return;
        _startNetworkTime       = msg.startNetworkTime;
        _dayLengthSeconds       = msg.dayLengthSeconds;
        _lunarCycleSeconds      = msg.lunarCycleSeconds;
        _fogStartDistance       = msg.fogStartDistance;
        _fogEndDistance         = msg.fogEndDistance;
        _age                    = msg.age;
        _ageStartedElapsedDays  = msg.ageStartedElapsedDays;
        _ageStartingYearDisplay = msg.ageStartingYearDisplay;

        if (!string.IsNullOrEmpty(msg.dayMonthNamesJson))
        {
            var names = JsonConvert.DeserializeObject<DayMonthNames>(msg.dayMonthNamesJson);
            SetDayMonthNames(names.d, names.m);
        }
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

    // ── Calendar read API (8.1.2) ───────────────────────────────────────────────────────────────────────

    public const int DaysPerWeek   = 7;
    public const int WeeksPerMonth = 4;
    public const int MonthsPerYear = 13;
    public const int DaysPerMonth  = DaysPerWeek * WeeksPerMonth;  // 28
    public const int DaysPerYear   = DaysPerMonth * MonthsPerYear; // 364

    /// <summary>8.1.2 (CAL3): eternal, never-resets elapsed day count — what DayFraction/LunarFraction are
    /// built on (unaffected by an age change). NOT what the calendar-display units below use.</summary>
    public static long WorldElapsedDays
    {
        get
        {
            if (_dayLengthSeconds <= 0f) return 0;
            double t = NetworkTime.time - _startNetworkTime;
            return (long)System.Math.Floor(t / _dayLengthSeconds);
        }
    }

    /// <summary>8.1.3 (CAL6a): elapsed days since the CURRENT age began. Every calendar-display unit
    /// (Day/Week/Month/Year) derives from this, not WorldElapsedDays, so a new age resets them all to 1
    /// while DayFraction (a separate computation) stays untouched.</summary>
    static long AgeElapsedDays => WorldElapsedDays - _ageStartedElapsedDays;

    public static int DayOfWeek   => (int)(AgeElapsedDays % DaysPerWeek) + 1;           // 1-7
    public static int DayOfMonth  => (int)(AgeElapsedDays % DaysPerMonth) + 1;          // 1-28
    public static int WeekOfYear  => (int)((AgeElapsedDays / DaysPerWeek) % (MonthsPerYear * WeeksPerMonth)) + 1; // 1-52
    public static int MonthOfYear => (int)((AgeElapsedDays / DaysPerMonth) % MonthsPerYear) + 1;                  // 1-13

    /// <summary>8.1.3 (CAL7): display year, not the raw internal 1-based count — offset by whatever year
    /// the current age was declared to start at (1372 by default for Age 1).</summary>
    public static int Year => _ageStartingYearDisplay + (int)(AgeElapsedDays / DaysPerYear);

    public static int  Age                   => _age;
    public static string DayOfWeekName       => _dayNames[Mathf.Clamp(DayOfWeek - 1, 0, DaysPerWeek - 1)];
    public static string MonthName           => _monthNames[Mathf.Clamp(MonthOfYear - 1, 0, MonthsPerYear - 1)];
    public static System.Collections.Generic.IReadOnlyList<string> DayNames   => _dayNames;
    public static System.Collections.Generic.IReadOnlyList<string> MonthNames => _monthNames;

    /// <summary>8.1.6 — shared ordinal-suffix formatter ("1st"/"2nd"/"3rd"/"4th", with the 11-13
    /// exception), used by both the /time command and /set-age's confirmation message.</summary>
    public static string OrdinalSuffix(int n) => (n % 100 is 11 or 12 or 13) ? "th"
        : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };

    // ── Debug override (Tools/World Clock Debug) ────────────────────────────────────────────────────────

    public static bool  DebugOverrideActive => _debugDayOverride >= 0f;
    public static void  SetDebugDayFraction(float f)   => _debugDayOverride = Mathf.Repeat(f, 1f);
    public static void  SetDebugLunarFraction(float f) => _debugLunarOverride = Mathf.Repeat(f, 1f);
    public static void  ClearDebugOverrides() { _debugDayOverride = -1f; _debugLunarOverride = -1f; }

    // ── /set-time chat command (testing convenience) ────────────────────────────────────────────────────

    /// <summary>Server-only: push the clock straight to a specific hour (0-23) — lets a tester jump
    /// straight to a time of day instead of waiting out a full day-length cycle. Rewrites the phase
    /// reference (<see cref="_startNetworkTime"/>) rather than the Tools/World Clock Debug override above,
    /// so it's genuinely server-authoritative: the caller broadcasts <see cref="BuildSync"/> afterward and
    /// every connected client (not just the host) picks up the new time. Also clears any active debug
    /// override, which would otherwise silently take priority and make this look like a no-op.
    ///
    /// 8.1.1 (CAL11): preserves the current elapsed-day count — the original implementation rewrote
    /// _startNetworkTime from the target fraction alone, discarding whatever whole-day count was
    /// previously implied. Harmless before the calendar existed; after 8.1, that would silently reset
    /// Age/Year/Month/Week to their Day-1 values on every /set-time call.</summary>
    public static void ServerSetHour(int hour)
    {
        ClearDebugOverrides();
        long currentDay = WorldElapsedDays; // capture BEFORE rewriting _startNetworkTime
        float targetFraction = Mathf.Clamp(hour, 0, 23) / 24f;
        double newElapsedSeconds = currentDay * (double)_dayLengthSeconds + targetFraction * _dayLengthSeconds;
        _startNetworkTime = NetworkTime.time - newElapsedSeconds;
    }

    // ── Age (8.1.3) ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Server-only, rare/deliberate: advance to a new Age. Resets the entire calendar display
    /// (Day/Week/Month/Year all read back as 1 immediately — CAL6a) while leaving DayFraction/time-of-day
    /// completely untouched (it never references the age anchor). Persists immediately rather than
    /// waiting for the 90s autosave tick — this is a rare action, not worth losing to an unlucky crash.
    /// Caller (NetworkedPlayer.CmdSetAge) is responsible for broadcasting BuildSync() afterward.</summary>
    public static void ServerSetAge(int age, int startingYearDisplay)
    {
        _age                    = age;
        _ageStartedElapsedDays  = WorldElapsedDays;
        _ageStartingYearDisplay = startingYearDisplay;
        ServerPersistCalendar();
    }

    // ── Persistence (8.1.1/8.1.2, CAL2) ─────────────────────────────────────────────────────────────────

    /// <summary>Server-only: write the current calendar state to the DB. Called on the 90s autosave tick
    /// and once more on clean shutdown (GameNetworkManager), plus immediately after ServerSetAge (a rare,
    /// deliberate action not worth risking to the 90s window). Direct synchronous write — matches
    /// WorldClockSettingsRepository's existing synchronous-read style; not routed through 1.2's async
    /// PersistenceService queue, which is built around the per-character ISaveJob convention and is
    /// unjustified machinery for a single-row settings write this infrequent.</summary>
    public static void ServerPersistCalendar()
    {
        // Best-effort: also called from GameNetworkManager.OnStopServer, which can run on the abort path
        // when the DB was never reachable in the first place (OnStartServer's catch calls StopServer()) —
        // swallow and log rather than let a second DB failure mask the original abort/crash the shutdown.
        try
        {
            double elapsedSeconds = NetworkTime.time - _startNetworkTime;
            using var conn = Database.OpenConnection();
            new WorldClockSettingsRepository().SaveCalendarState(
                conn, elapsedSeconds, _age, _ageStartedElapsedDays, _ageStartingYearDisplay);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[WorldClock] ServerPersistCalendar failed — will retry on the next tick: {e.Message}");
        }
    }
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
