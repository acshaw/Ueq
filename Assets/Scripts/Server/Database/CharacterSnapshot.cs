using System.Collections.Generic;

/// <summary>
/// Plain, Unity-free snapshot of a character's persisted state (1.3). This is the only object that
/// crosses the main-thread → worker-thread boundary: it is captured from live components on the main
/// thread (<see cref="CharacterPersistence.CaptureSnapshot"/>) and written by the worker, or read by
/// the worker and re-applied on the main thread. It must never reference Mirror/Unity objects.
///
/// Only the *mutable, non-derived* state is here — level, stats, max HP/mana, and known abilities are
/// reconstructed from XP + race/class + equipment on load (see the devplan's persist-vs-reconstruct
/// table).
/// </summary>
public sealed class CharacterSnapshot
{
    public long   AccountId;
    public long   CharacterId;          // 0 until the row exists; set by Load
    public string Name = "";

    public string RaceName;
    public string ClassName;

    public int    TotalXp;
    public int    Copper, Silver, Gold, Platinum;
    public int    CurrentHealth, CurrentMana;

    public float  PosX, PosY, PosZ, Yaw;
    public float  BindX, BindY, BindZ;

    public string ZoneId = ZoneCatalog.DefaultStarterZoneId;  // M3.0 — which zone the character last stood in

    public string ActualRace   = "";
    public string ApparentRace = "";

    public InvEntry[]               Inventory     = new InvEntry[0];   // fixed length = inventory slot count
    public string[]                 Equipment     = new string[0];     // fixed length = equip slot count
    public Dictionary<string, int>  FactionScores = new();
    public string[]                 Hotbar        = new string[0];     // fixed length = hotbar size
}

/// <summary>One inventory slot as plain data — keeps the Mirror <c>InventorySlot</c> serializer off
/// the worker thread.</summary>
public struct InvEntry
{
    public string Id;
    public int    Q;
}
