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
    // 2026-08-21 (Mitigation) — AC is the sole mitigation lever; equipment-only, no class/race base.
    public int bonusAc = 0;

    [Header("Weapon Stats")]
    public int            weaponBaseDamage  = 10;
    // 2026-08-21 — the stat-scalable portion of this weapon's damage: Damage = (RelevantStat x 0.01 x
    // weaponBonusDamage) + weaponBaseDamage. weaponBaseDamage stays flat regardless of STR/DEX.
    public int            weaponBonusDamage = 0;
    public float          weaponDelay      = 2f;
    public float          weaponRange      = 3f;
    public WeaponCategory weaponCategory   = WeaponCategory.Might;

    [Header("Economy")]
    public int buyPrice  = 0;  // copper — 0 = not sold by vendors
    public int sellPrice = 0;  // copper — 0 = vendors won't buy this

    [Header("Flags")]
    // 3.2.1 — LORE (EQ1-style): the item can be possessed at most once (inventory + equipped). Enforced
    // server-side in PlayerInventory on external acquire paths (loot / vendor buy / quest reward). Implies
    // max-one regardless of maxStackSize. The anti-farm lever for 3.2's repeatable item-reward quests.
    public bool lore = false;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(itemId))
            itemId = name;
    }
#endif
}
