using System.Collections.Generic;
using UnityEngine;

public enum ManaStatType { None, Intellect, Wisdom }

[CreateAssetMenu(menuName = "Ueq/Class Definition")]
public class ClassDefinition : ScriptableObject
{
    public string className  = "Warrior";
    public float  xpModifier = 1f;

    [Header("Base Stats")]
    public int baseStr = 10;
    public int baseSta = 10;
    public int baseAgi = 10;
    public int baseDex = 10;
    public int baseInt = 10;
    public int baseWis = 10;
    public int baseCha = 10;

    [Header("HP Formula")]
    public int   classBaseHP   = 15;
    public int   hpPerLevel    = 4;
    public int   staCap        = 255;
    public float baseStaRatio  = 0.23f;
    public float staGrowthRate = 0.15f;

    [Header("Mana Formula")]
    public ManaStatType manaStatType  = ManaStatType.None;
    public int   classBaseMana  = 0;
    public int   manaPerLevel   = 0;
    public int   manaCap        = 0;
    public float baseManaRatio  = 0.23f;
    public float manaGrowthRate = 0f;

    [Header("Combat Tier Table (5.1.1)")]
    [Tooltip("Level 1 hit-tier weighted table (design doc §2.5). Seeded via Tools/Combat/Seed Class " +
             "Combat Tables — Warrior's numbers are given verbatim by the doc; Cleric/Wizard are a " +
             "budget-ratio-scaled placeholder pending real hand-authored tables.")]
    public CombatTierTable combatTierTableLevel1 = CombatTierTable.WarriorLevel1;
    [Tooltip("Level 20 target table (design doc §2.11 for Warrior; scaled placeholder for other " +
             "classes). CombatResolver interpolates Level 1 → Level 20 by the character's level.")]
    public CombatTierTable combatTierTableLevel20 = new()
    {
        Miss = 2f, Glancing = 13f, Hit = 20f, SolidHit = 35f, GoodHit = 25f, Critical = 3f, Crippling = 2f,
    };

    [Header("Abilities")]
    public List<AbilityDefinition> startingAbilities = new();

    [Header("Weapon Prop (3.1.6)")]
    [Tooltip("Cosmetic weapon attached to the body's right-hand bone (Warrior sword / Wizard staff / " +
             "Cleric sceptre). Shown in the create preview and in-world. Leave empty for no prop, or for a " +
             "body that already ships a held weapon.")]
    public GameObject weaponPropPrefab;
    [Tooltip("Local position offset of the prop relative to the right-hand bone (tune live in the 3.1.6 preview).")]
    public Vector3 gripPositionOffset;
    [Tooltip("Local euler-angle offset of the prop relative to the right-hand bone (tune live in the preview).")]
    public Vector3 gripEulerOffset;
}
