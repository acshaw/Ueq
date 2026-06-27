using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Server-only lookup of spawn tables by id (M2.7.2). Builds a runtime <see cref="SpawnTable"/> per DB
/// row — the same type the SO path used — so <c>SpawnPoint</c> rolls it unchanged; only the source moved
/// off ScriptableObjects. Each entry's <c>mob</c> resolves its id → the runtime <c>MobDefinition</c> via
/// <see cref="MobRegistry"/>, and the inlined timer becomes a runtime <c>SpawnTimer</c>. No client sync.
/// Populated by <c>ContentLoader</c> <b>after mobs</b> (entries resolve mob defs).
/// </summary>
public static class SpawnTableRegistry
{
    static readonly Dictionary<string, SpawnTable> _byId = new();

    public static void LoadFrom(IEnumerable<SpawnTableSnapshot> snapshots)
    {
        _byId.Clear();
        foreach (var s in snapshots)
            if (!string.IsNullOrEmpty(s.SpawnTableId))
                _byId[s.SpawnTableId] = Build(s);
    }

    public static SpawnTable Get(string spawnTableId)
        => string.IsNullOrEmpty(spawnTableId) ? null : _byId.GetValueOrDefault(spawnTableId);

    public static int Count => _byId.Count;

    static SpawnTable Build(SpawnTableSnapshot s)
    {
        var table = ScriptableObject.CreateInstance<SpawnTable>();
        table.name = s.SpawnTableId;

        var timer = ScriptableObject.CreateInstance<SpawnTimer>();
        timer.name        = s.SpawnTableId + " Timer";
        timer.baseSeconds = s.TimerBaseSeconds;
        timer.variance    = s.TimerVariance;
        table.defaultTimer = timer;

        table.entries = new List<SpawnTableEntry>();
        foreach (var e in s.Entries)
        {
            var mob = MobRegistry.Get(e.MobId);
            if (mob == null)
            {
                Debug.LogWarning($"[Content] Spawn table '{s.SpawnTableId}' references mob '{e.MobId}' " +
                                 "which is not in the mob registry — that entry is skipped.");
                continue;
            }
            table.entries.Add(new SpawnTableEntry { mob = mob, weight = e.Weight, groupSize = e.GroupSize });
        }
        return table;
    }
}
