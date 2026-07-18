/// <summary>Plain-data view of one race (M2.10). Mirrors RaceDefinition.</summary>
public struct RaceSnapshot
{
    public string RaceId;
    public string RaceName;
    public float  XpModifier;
    public int    StrMod, StaMod, AgiMod, DexMod, IntMod, WisMod, ChaMod;
}
