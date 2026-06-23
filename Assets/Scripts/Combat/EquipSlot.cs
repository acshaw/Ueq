public enum EquipSlot
{
    Head    = 0,
    Chest   = 1,
    Legs    = 2,
    Hands   = 3,
    Feet    = 4,
    Back    = 5,
    Neck    = 6,
    Ring1   = 7,
    Ring2   = 8,
    Ear1    = 9,
    Ear2    = 10,
    Weapon  = 11,
    Offhand = 12,
}

public static class EquipSlotUtil
{
    public const int Count = 13;

    public static string DisplayName(this EquipSlot slot) => slot switch
    {
        EquipSlot.Ring1   => "Ring 1",
        EquipSlot.Ring2   => "Ring 2",
        EquipSlot.Ear1    => "Ear 1",
        EquipSlot.Ear2    => "Ear 2",
        _                 => slot.ToString(),
    };
}
