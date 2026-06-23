using UnityEngine;

[CreateAssetMenu(menuName = "Ueq/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    public string itemId      = "";
    public string displayName = "Unknown Item";
    [TextArea(2, 4)]
    public string description = "";
    public int    maxStackSize = 1;

    public bool IsStackable => maxStackSize > 1;

    [Header("Equipment")]
    public bool        isEquippable = false;
    public EquipSlot   equipSlot    = EquipSlot.Weapon;

    [Header("Stat Bonuses")]
    public int bonusStr, bonusSta, bonusAgi, bonusDex, bonusInt, bonusWis, bonusCha;

    [Header("Weapon Stats")]
    public int            weaponBaseDamage = 10;
    public float          weaponDelay      = 2f;
    public float          weaponRange      = 3f;
    public WeaponCategory weaponCategory   = WeaponCategory.Might;

    [Header("Economy")]
    public int buyPrice  = 0;  // copper — 0 = not sold by vendors
    public int sellPrice = 0;  // copper — 0 = vendors won't buy this

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(itemId))
            itemId = name;
    }
#endif
}
