using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime access to the single <see cref="CharacterRoster"/> asset (3.1.4, RM1). Loaded from
/// <c>Resources/CharacterRoster</c> on first use, available on both server (validation) and client
/// (body-model resolution). Mirrors the <see cref="RaceClassRegistry"/> pattern; M2.10 can swap the
/// backing store for the DB content loader behind the same lookups without touching callers.
/// </summary>
public static class CharacterRosterRegistry
{
    static CharacterRoster _roster;

    static CharacterRoster Roster
    {
        get
        {
            if (_roster == null) _roster = Resources.Load<CharacterRoster>("CharacterRoster");
            return _roster;
        }
    }

    static IEnumerable<RosterEntry> Entries
        => Roster != null && Roster.entries != null ? (IEnumerable<RosterEntry>)Roster.entries : System.Array.Empty<RosterEntry>();

    /// <summary>Drop the cache so the next lookup reloads (useful after editing the asset in-editor).</summary>
    public static void Invalidate() => _roster = null;

    /// <summary>Distinct genders offered, in enum order.</summary>
    public static Gender[] Genders()
    {
        var seen = new List<Gender>();
        foreach (var e in Entries) if (!seen.Contains(e.gender)) seen.Add(e.gender);
        seen.Sort();
        return seen.ToArray();
    }

    /// <summary>Distinct races available to a gender, first-seen order.</summary>
    public static string[] RacesFor(Gender gender)
    {
        var list = new List<string>();
        foreach (var e in Entries)
            if (e.gender == gender && !string.IsNullOrEmpty(e.race) && !list.Contains(e.race))
                list.Add(e.race);
        return list.ToArray();
    }

    /// <summary>Distinct classes available to a (gender, race), first-seen order.</summary>
    public static string[] ClassesFor(Gender gender, string race)
    {
        var list = new List<string>();
        foreach (var e in Entries)
            if (e.gender == gender && e.race == race && !string.IsNullOrEmpty(e.cls) && !list.Contains(e.cls))
                list.Add(e.cls);
        return list.ToArray();
    }

    /// <summary>Server-side whitelist check: is this exact tuple a legal, authored combination?</summary>
    public static bool IsValid(Gender gender, string race, string cls)
    {
        foreach (var e in Entries)
            if (e.gender == gender && e.race == race && e.cls == cls) return true;
        return false;
    }

    /// <summary>Client-side body resolver: the Synty prefab for a tuple, or null if unauthored.</summary>
    public static GameObject GetModel(Gender gender, string race, string cls)
    {
        foreach (var e in Entries)
            if (e.gender == gender && e.race == race && e.cls == cls) return e.modelPrefab;
        return null;
    }

    /// <summary>Every legal tuple, for the client create form (sent in CharacterListMessage).</summary>
    public static CreateOption[] AllOptions()
    {
        var list = new List<CreateOption>();
        foreach (var e in Entries)
            list.Add(new CreateOption { gender = e.gender.ToString(), race = e.race, cls = e.cls });
        return list.ToArray();
    }
}
