using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Faction/Threshold Table")]
public class FactionThresholdTable : ScriptableObject
{
    [Tooltip("Ordered lowest MinScore to highest")]
    public List<FactionThreshold> Thresholds;

    // Returns the highest threshold whose MinScore the given score meets.
    public FactionThreshold Evaluate(int score)
    {
        FactionThreshold result = Thresholds[0];
        foreach (var t in Thresholds)
        {
            if (score >= t.MinScore) result = t;
            else break;
        }
        return result;
    }

    public int IndexOf(string name)
    {
        for (int i = 0; i < Thresholds.Count; i++)
            if (Thresholds[i].Name == name) return i;
        return -1;
    }
}
