using System.Collections.Generic;
using UnityEngine;

public enum MovementType { Stationary, Wander }

[CreateAssetMenu(menuName = "Ueq/Mob Definition")]
public class MobDefinition : ScriptableObject
{
    [Header("Identity")]
    public string     displayName = "Unnamed Mob";
    public int        mobLevel    = 1;

    // 3.1.10 Stage 0: the visual body is a plain art prefab loaded at runtime from
    // Resources/MobModels/<modelId> (see MobModel) — no per-mob networked prefab. Blank = use the mob id
    // (convention), so naming the art prefab to match the mob id needs no explicit value; an explicit id
    // lets several mobs share one body (e.g. rat variants → one "giant_rat" model).
    public string     modelId     = "";

    // The networked Mirror spawnable to instantiate — almost always the shared "Enemy". Distinct art no
    // longer needs a distinct prefab (that's modelId now); reserve this for genuinely different net setups.
    public GameObject prefab;

    [Header("Combat")]
    public int   maxHealth      = 10;
    public int   attackDamage   = 1;
    public float attackInterval = 2f;
    public float attackRange    = 2f;

    // 5.1.1 (HR5) / 2.12 (SK5): mobs get the full symmetric combat pipeline, authored per-mob rather
    // than derived from mobLevel. weaponCategory/weaponSkill feed Step 1's Skill Differential the same
    // way a player's PlayerWeaponSkills does; combatTable is this mob's own hit-tier weighted table
    // (not interpolated by level — a flat authored value, unlike a player's class-table interpolation).
    public WeaponCategory  weaponCategory = WeaponCategory.Might;
    public int             weaponSkill    = 0;
    public CombatTierTable combatTable    = CombatTierTable.WarriorLevel1;

    // 5.1.2 (AV3): whether this mob's attack can be Parried — false for beast/unarmed-style attacks
    // (a lion bite cannot be parried; a sword swing can). Riposte/Dodge are unaffected by this flag.
    public bool attackIsParryable = true;

    // 5.1.2: mobs have no CharacterStats, so their Dodge/Parry/Riposte avoidance rolls read these
    // authored stand-ins instead of Agility/Dexterity. Defaults to a low, generic baseline — bump per
    // mob for anything meant to read as more evasive.
    public float avoidanceAgility   = 20f;
    public float avoidanceDexterity = 20f;

    [Header("Movement")]
    public MovementType movementType   = MovementType.Wander;
    public float        moveSpeed      = 3.5f;
    public float        wanderRadius   = 10f;
    public float        wanderPauseMin = 2f;
    public float        wanderPauseMax = 6f;

    [Header("AI")]
    public float perceptionRadius = 20f;
    public int   baseAggroThreat  = 1;

    [Header("Faction")]
    // M2.5: DB mobs reference a faction by id (resolved via FactionRegistry once factions land at 2.6);
    // the SO ref is set at runtime when resolvable. Until then `faction` stays null (no faction behavior).
    public string            factionId = "";
    public FactionDefinition faction;
    public string aggroMaxStanding   = "Threatening";
    public string warningMaxStanding = "Apprehensive";

    [Header("Conversation")]
    // M2.4: conversation sets live in the DB. `conversationSetId` references a conversation_sets row
    // (resolved at runtime via ConversationRegistry).
    public string                 conversationSetId = "";

    [Header("Loot")]
    // M2.5: DB mobs reference a loot table by id (resolved once loot tables land at 2.7); SO ref set
    // at runtime when resolvable. Until then `lootTable` stays null (no drops).
    public string    lootTableId = "";
    public LootTable lootTable;

    [Header("Rewards")]
    public int xpReward     = 0;

    // M2.7.1: faction standing changes applied to the killing player on death. delta < 0 worsens,
    // > 0 improves. `faction` is resolved from `factionId` at build (via FactionRegistry); a hit with an
    // unresolved faction or delta == 0 is skipped. Authored per mob (kill consequence, not loot).
    [System.Serializable]
    public struct FactionHit
    {
        public string            factionId;
        public FactionDefinition faction;
        public int               delta;
    }
    public List<FactionHit> factionHits = new();

    [Header("Vendor")]
    // M2.3: vendors live in the DB. `vendorId` references a vendor_inventories row (resolved at runtime
    // via VendorRegistry).
    public string          vendorId = "";
    public string          vendorOpenKeyword = "wares";
}
