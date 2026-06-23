using UnityEngine;

[CreateAssetMenu(menuName = "Ueq/Race Definition")]
public class RaceDefinition : ScriptableObject
{
    public string raceName   = "Human";
    public float  xpModifier = 1f;

    [Header("Stat Modifiers")]
    public int strMod;
    public int staMod;
    public int agiMod;
    public int dexMod;
    public int intMod;
    public int wisMod;
    public int chaMod;
}
