using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class VendorEntry
{
    public ItemDefinition item;
}

[CreateAssetMenu(menuName = "Ueq/Vendor Inventory")]
public class VendorInventory : ScriptableObject
{
    public List<VendorEntry> Entries = new();
}
