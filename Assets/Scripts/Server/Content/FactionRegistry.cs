using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Server-only lookup of factions by id (M2.6). Builds runtime <see cref="FactionDefinition"/> instances
/// from the DB — the same type the SO path used — so every consumer (<c>NpcFaction</c>,
/// <c>PlayerFactionScores</c>, <c>MobDefinition.faction</c>) is unchanged; only the source moved off
/// ScriptableObjects. No client sync (faction evaluation is server-side; only player score numbers sync).
///
/// Populating this flips the already-wired gates live: <c>MobRegistry.Build</c> resolves
/// <c>def.faction = Get(factionId)</c> and <c>NpcConversation.MeetsFactionRequirement</c> resolves
/// <c>required_faction_id</c> through <see cref="Get"/>. Loaded by <c>ContentLoader</c> <b>before mobs</b>.
/// </summary>
public static class FactionRegistry
{
    static readonly Dictionary<string, FactionDefinition> _byId = new();

    /// <summary>The single shared standing ladder (DC4) all factions reference.</summary>
    public static FactionThresholdTable Thresholds { get; private set; }

    /// <summary>Runtime race→faction starting-score table, referencing the built faction instances.</summary>
    public static RaceDefaultsTable RaceDefaults { get; private set; }

    /// <summary>Build the shared threshold table, the factions, their relations, and race defaults.</summary>
    public static void LoadFrom(FactionContent content)
    {
        _byId.Clear();

        // 1) Shared threshold ladder.
        Thresholds = ScriptableObject.CreateInstance<FactionThresholdTable>();
        Thresholds.name = "DbThresholds";
        Thresholds.Thresholds = (content.Thresholds ?? new List<FactionThresholdSnapshot>())
            .OrderBy(t => t.SortOrder)
            .Select(t => new FactionThreshold { Name = t.Name, MinScore = t.MinScore })
            .ToList();

        // 2) Faction instances (empty relation lists; FactionId is the score/lookup key per DF2).
        foreach (var f in content.Factions ?? new List<FactionSnapshot>())
        {
            if (string.IsNullOrEmpty(f.FactionId)) continue;
            var def = ScriptableObject.CreateInstance<FactionDefinition>();
            def.name            = f.FactionId;
            def.FactionId       = f.FactionId;
            def.FactionName     = f.FactionName;
            def.ThresholdTable  = Thresholds;
            def.AlliedFactions  = new List<FactionDefinition>();
            def.HostileFactions = new List<FactionDefinition>();
            _byId[f.FactionId] = def;
        }

        // 3) Wire relations (factions reference each other → second pass) + race defaults.
        foreach (var f in content.Factions ?? new List<FactionSnapshot>())
        {
            if (!_byId.TryGetValue(f.FactionId, out var def)) continue;
            foreach (var rel in f.Relations ?? new List<FactionRelationSnapshot>())
            {
                if (!_byId.TryGetValue(rel.OtherFactionId, out var other)) continue;
                if (rel.Relation == "ally")         def.AlliedFactions.Add(other);
                else if (rel.Relation == "hostile") def.HostileFactions.Add(other);
            }
        }

        RaceDefaults = ScriptableObject.CreateInstance<RaceDefaultsTable>();
        RaceDefaults.name = "DbRaceDefaults";
        RaceDefaults.Defaults = new List<RaceDefaultEntry>();
        foreach (var d in content.RaceDefaults ?? new List<RaceFactionDefaultSnapshot>())
            if (_byId.TryGetValue(d.FactionId, out var fac))
                RaceDefaults.Defaults.Add(new RaceDefaultEntry { Race = d.Race, Faction = fac, Score = d.Score });
    }

    public static FactionDefinition Get(string factionId)
        => string.IsNullOrEmpty(factionId) ? null : _byId.GetValueOrDefault(factionId);

    public static int Count => _byId.Count;
    public static bool IsPopulated => _byId.Count > 0;
}
