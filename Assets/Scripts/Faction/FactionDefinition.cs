using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Faction/Faction Definition")]
public class FactionDefinition : ScriptableObject
{
    public string FactionName;
    public FactionThresholdTable ThresholdTable;

    [Header("NPC-to-NPC Relations")]
    public List<FactionDefinition> AlliedFactions;
    public List<FactionDefinition> HostileFactions;

    public bool IsAllyWith(FactionDefinition other) => AlliedFactions.Contains(other);
    public bool IsHostileWith(FactionDefinition other) => HostileFactions.Contains(other);
}
