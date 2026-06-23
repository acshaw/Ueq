using Mirror;
using UnityEngine;

[System.Serializable]
public struct InventorySlot
{
    public string itemId;
    public int    quantity;

    public bool IsEmpty => string.IsNullOrEmpty(itemId) || quantity <= 0;

    public static InventorySlot Empty => new InventorySlot { itemId = "", quantity = 0 };
}

public static class InventorySlotSerializer
{
    public static void WriteInventorySlot(this NetworkWriter writer, InventorySlot slot)
    {
        writer.WriteString(slot.itemId);
        writer.WriteInt(slot.quantity);
    }

    public static InventorySlot ReadInventorySlot(this NetworkReader reader)
    {
        return new InventorySlot
        {
            itemId   = reader.ReadString(),
            quantity = reader.ReadInt(),
        };
    }
}
