using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnTableEntry
{
    public MobDefinition mob;
    public int           weight    = 1;
    public int           groupSize = 1; // reserved — group spawning not yet implemented
}

[CreateAssetMenu(menuName = "Ueq/Spawn Table")]
public class SpawnTable : ScriptableObject
{
    public List<SpawnTableEntry> entries      = new();
    public SpawnTimer            defaultTimer;

    public SpawnTableEntry Roll()
    {
        int total = 0;
        foreach (var e in entries)
            if (e.mob != null) total += e.weight;

        if (total == 0) return null;

        int roll       = Random.Range(0, total);
        int cumulative = 0;
        foreach (var e in entries)
        {
            if (e.mob == null) continue;
            cumulative += e.weight;
            if (roll < cumulative) return e;
        }
        return null;
    }
}
