using UnityEngine;

public enum MovementType { Stationary, Wander }

[CreateAssetMenu(menuName = "Ueq/Mob Definition")]
public class MobDefinition : ScriptableObject
{
    [Header("Identity")]
    public string     displayName = "Unnamed Mob";
    public int        mobLevel    = 1;
    public GameObject prefab;

    [Header("Combat")]
    public int   maxHealth      = 10;
    public int   attackDamage   = 1;
    public float attackInterval = 2f;
    public float attackRange    = 2f;

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
    public FactionDefinition faction;
    public string aggroMaxStanding   = "Threatening";
    public string warningMaxStanding = "Apprehensive";

    [Header("Conversation")]
    public ConversationKeywordSet conversationKeywordSet;

    [Header("Loot")]
    public LootTable lootTable;

    [Header("Rewards")]
    public int xpReward     = 0;

    [Header("Vendor")]
    public VendorInventory vendorInventory;
    public string          vendorOpenKeyword = "wares";
}
