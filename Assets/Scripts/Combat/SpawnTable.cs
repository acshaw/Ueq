using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnTableEntry
{
    public MobDefinition mob;
    public int           weight    = 1;
    public int           groupSize = 1; // mobs spawned per activation (implemented M2.7.2)

    // 8.1.4 — calendar-aware spawn conditions. "Any" / null = no restriction (existing behavior,
    // unaffected). time/lunar are small fixed enums (string, matches how mobs.aggro_max_standing is
    // already stored); day-of-week/month are numeric ranges, so nullable ints instead.
    public string timeCondition      = "Any"; // Any | DayOnly | NightOnly
    public string lunarCondition     = "Any"; // Any | FullMoonOnly | NewMoonOnly
    public int?   dayOfWeekCondition;         // 1-7, null = Any
    public int?   monthCondition;             // 1-13, null = Any

    // 8.2 (NR1) — optional per-entry respawn pace override, so a named/rare roll can pop back in
    // slower than the trash it replaces. null = fall back to the table's defaultTimer (unchanged
    // behavior for every entry that doesn't set one).
    public float? respawnBaseSeconds;
    public float? respawnVariance;

    // 8.3 (LV3) — optional per-entry level range. Both null = no variance, spawns at the mob's authored
    // MobDefinition.mobLevel exactly (unchanged existing behavior).
    public int? minLevelOverride;
    public int? maxLevelOverride;
}

[CreateAssetMenu(menuName = "Ueq/Spawn Table")]
public class SpawnTable : ScriptableObject
{
    public List<SpawnTableEntry> entries      = new();
    public SpawnTimer            defaultTimer;

    // 8.1.4 (TG1) — day/night boundary matches 5.12's star-visibility boundary (starHideStart = 0.25),
    // so "day" means the same thing to a player as it already does visually.
    const float DayStart = 0.25f, DayEnd = 0.75f;

    static bool IsDay() => WorldClock.DayFraction >= DayStart && WorldClock.DayFraction < DayEnd;

    static bool IsEligible(SpawnTableEntry e)
    {
        if (e.timeCondition == "DayOnly" && !IsDay()) return false;
        if (e.timeCondition == "NightOnly" && IsDay()) return false;
        if (e.lunarCondition == "FullMoonOnly" && WorldClock.CurrentLunarPhase != LunarPhase.Full) return false;
        if (e.lunarCondition == "NewMoonOnly" && WorldClock.CurrentLunarPhase != LunarPhase.New) return false;
        if (e.dayOfWeekCondition.HasValue && e.dayOfWeekCondition.Value != WorldClock.DayOfWeek) return false;
        if (e.monthCondition.HasValue && e.monthCondition.Value != WorldClock.MonthOfYear) return false;
        return true;
    }

    /// <summary>8.1.4 (TG2/TG3): filters to currently-eligible entries first, then weights among
    /// survivors — not a post-hoc reroll, which would bias toward whichever entry is checked first.
    /// <paramref name="anyEntriesExist"/> distinguishes "resolved to nothing because everything's
    /// condition-filtered right now" (still true, quiet/expected) from "genuinely empty/misconfigured"
    /// (false) — SpawnPoint uses this to avoid warning every 5s for a table that's simply night-only
    /// during the day.</summary>
    public SpawnTableEntry Roll(out bool anyEntriesExist)
    {
        anyEntriesExist = false;
        int total = 0;
        foreach (var e in entries)
        {
            if (e.mob == null) continue;
            anyEntriesExist = true;
            if (!IsEligible(e)) continue;
            total += e.weight;
        }

        if (total == 0) return null;

        int roll       = Random.Range(0, total);
        int cumulative = 0;
        foreach (var e in entries)
        {
            if (e.mob == null || !IsEligible(e)) continue;
            cumulative += e.weight;
            if (roll < cumulative) return e;
        }
        return null;
    }
}
