using UnityEngine;

/// <summary>
/// 5.12 (DC2) — tunable day-length / lunar-cycle-length config. Resources-loaded with an in-memory
/// fallback if no asset is authored, same convention as <see cref="XpTableDefinition"/> and
/// <see cref="CombatTierDamageConfig"/>. A single global tunable, not per-item content — no DB/web editor.
/// </summary>
[CreateAssetMenu(menuName = "Ueq/World Clock Settings")]
public class WorldClockSettings : ScriptableObject
{
    [Tooltip("Real-world minutes for one full in-game day/night cycle.")]
    [Min(1f)] public float dayLengthMinutes = 50f;

    [Tooltip("In-game days for one full lunar cycle (new moon to new moon).")]
    [Min(0.5f)] public float lunarCycleDays = 8f;

    public float DayLengthSeconds  => Mathf.Max(1f, dayLengthMinutes * 60f);
    public float LunarCycleSeconds => Mathf.Max(1f, lunarCycleDays) * DayLengthSeconds;

    /// <summary>Resources path (no extension) WorldClock loads this from.</summary>
    public const string ResourcePath = "WorldClockSettings";

    static WorldClockSettings _cache;

    /// <summary>Resources asset if authored (Create Asset menu → Resources/WorldClockSettings.asset to
    /// tune in the Inspector); otherwise an in-memory instance carrying the defaults above.</summary>
    public static WorldClockSettings Active
    {
        get
        {
            if (_cache != null) return _cache;
            _cache = Resources.Load<WorldClockSettings>(ResourcePath);
            if (_cache == null) _cache = CreateInstance<WorldClockSettings>();
            return _cache;
        }
    }
}
