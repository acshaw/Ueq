using UnityEngine;
using UnityEngine.AI;

// Holds the MobDefinition for this enemy instance.
// Sibling components cache a reference to this in Awake and pull their values from it.
[RequireComponent(typeof(NavMeshAgent))]
public class MobApplicator : MonoBehaviour
{
    [SerializeField] MobDefinition definition;
    public MobDefinition Definition => definition;

    // 8.3 (LV2) — per-instance level override, set by SpawnPoint when a spawn table entry has a level
    // range configured. Lives here, not on MobDefinition — that's a single runtime instance shared by
    // every concurrent spawn of this mob id, so writing an override there would leak one instance's roll
    // into every other instance of the same mob type currently alive anywhere in the world.
    public int? LevelOverride { get; private set; }
    public void SetLevelOverride(int? level) => LevelOverride = level;

    void Awake() => Apply();

    // Called by SpawnPoint after Instantiate to override the prefab's default definition
    public void SetDefinition(MobDefinition def)
    {
        definition = def;
        Apply();
    }

    void Apply()
    {
        if (definition == null) return;
        gameObject.name = definition.displayName;
        GetComponent<NavMeshAgent>().speed = definition.moveSpeed;
        ApplyMovementBehavior();
    }

    void ApplyMovementBehavior()
    {
        var wander     = GetComponent<WanderBehavior>();
        var stationary = GetComponent<StationaryBehavior>();

        switch (definition.movementType)
        {
            case MovementType.Stationary:
                if (wander != null)     DestroyImmediate(wander);
                if (stationary == null) gameObject.AddComponent<StationaryBehavior>();
                break;

            case MovementType.Wander:
                if (stationary != null) DestroyImmediate(stationary);
                if (wander == null)     gameObject.AddComponent<WanderBehavior>();
                break;
        }
    }
}
