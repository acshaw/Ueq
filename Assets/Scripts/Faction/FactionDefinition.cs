using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Faction/Faction Definition")]
public class FactionDefinition : ScriptableObject
{
    // Stable id — the key used for scores, persistence, and mob/conversation references (M2.6).
    // DB-built instances set this; legacy SO assets leave it blank and fall back to FactionName via Key.
    public string FactionId;
    public string FactionName;          // display label

    /// <summary>The score / persistence / lookup key: FactionId when set, else FactionName (SO fallback).</summary>
    public string Key => string.IsNullOrEmpty(FactionId) ? FactionName : FactionId;

    public FactionThresholdTable ThresholdTable;

    [Header("NPC-to-NPC Relations")]
    public List<FactionDefinition> AlliedFactions;
    public List<FactionDefinition> HostileFactions;

    public bool IsAllyWith(FactionDefinition other) => AlliedFactions.Contains(other);
    public bool IsHostileWith(FactionDefinition other) => HostileFactions.Contains(other);
}
