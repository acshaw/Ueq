using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct RaceDefaultEntry
{
    public string Race;
    public FactionDefinition Faction;
    public int Score;
}

[CreateAssetMenu(menuName = "Faction/Race Defaults Table")]
public class RaceDefaultsTable : ScriptableObject
{
    public List<RaceDefaultEntry> Defaults;

    public int GetDefault(string race, FactionDefinition faction)
    {
        foreach (var entry in Defaults)
            if (entry.Race == race && entry.Faction == faction)
                return entry.Score;
        return 0; // Indifferent
    }
}
