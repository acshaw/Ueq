using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves a stored race/class <i>name</i> back to its <see cref="RaceDefinition"/> /
/// <see cref="ClassDefinition"/> asset (1.3). Character persistence stores only the identifier; this
/// is the seam that reconstructs everything derived from it (stats, HP/mana, known abilities).
///
/// Loads from <c>Resources/Races</c> and <c>Resources/Classes</c> on first use (assets live there so
/// they're discoverable at runtime). M2.6 swaps this backing store for the DB content loader behind
/// the same two lookups — callers don't change.
/// </summary>
public static class RaceClassRegistry
{
    static Dictionary<string, RaceDefinition>  _races;
    static Dictionary<string, ClassDefinition> _classes;

    public static RaceDefinition GetRace(string name)
    {
        EnsureLoaded();
        return string.IsNullOrEmpty(name) ? null : _races.GetValueOrDefault(name);
    }

    public static ClassDefinition GetClass(string name)
    {
        EnsureLoaded();
        return string.IsNullOrEmpty(name) ? null : _classes.GetValueOrDefault(name);
    }

    /// <summary>All known race names (for the character-creation form, 1.5).</summary>
    public static string[] AllRaceNames()
    {
        EnsureLoaded();
        return new List<string>(_races.Keys).ToArray();
    }

    /// <summary>All known class names (for the character-creation form, 1.5).</summary>
    public static string[] AllClassNames()
    {
        EnsureLoaded();
        return new List<string>(_classes.Keys).ToArray();
    }

    /// <summary>Drop the cache so the next lookup reloads (useful after editing assets in-editor).</summary>
    public static void Invalidate()
    {
        _races   = null;
        _classes = null;
    }

    static void EnsureLoaded()
    {
        if (_races != null && _classes != null) return;

        _races = new Dictionary<string, RaceDefinition>();
        foreach (var r in Resources.LoadAll<RaceDefinition>("Races"))
            if (r != null && !string.IsNullOrEmpty(r.raceName))
                _races[r.raceName] = r;

        _classes = new Dictionary<string, ClassDefinition>();
        foreach (var c in Resources.LoadAll<ClassDefinition>("Classes"))
            if (c != null && !string.IsNullOrEmpty(c.className))
                _classes[c.className] = c;
    }
}
