using UnityEngine;

/// <summary>
/// Runtime-only since M2.10 — built by <see cref="RaceClassRegistry"/> from a DB-backed
/// <see cref="RaceSnapshot"/> (server load or client catalog sync). No longer authored as an asset;
/// author races in the web Race Editor.
/// </summary>
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
