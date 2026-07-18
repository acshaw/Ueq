namespace Ueq.ContentApi.Models;

/// <summary>EF entity for <c>races</c> (M2.10). Mapping-only; SQL runner owns the schema.</summary>
public class Race
{
    public string RaceId { get; set; } = string.Empty;
    public string RaceName { get; set; } = string.Empty;
    public float XpModifier { get; set; } = 1f;
    public int StrMod { get; set; }
    public int StaMod { get; set; }
    public int AgiMod { get; set; }
    public int DexMod { get; set; }
    public int IntMod { get; set; }
    public int WisMod { get; set; }
    public int ChaMod { get; set; }
    public DateTime UpdatedAt { get; set; }
}
