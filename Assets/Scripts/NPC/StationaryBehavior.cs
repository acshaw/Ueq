public class StationaryBehavior : UnityEngine.MonoBehaviour, INpcMovementBehavior
{
    public void Startup() { }
    public void Suspend() { }
    public void Resume()  { }

    // 3.1.11 (WR5): a stationary NPC always returns to its spawn spot.
    public UnityEngine.Vector3 GetReturnAnchor(UnityEngine.Vector3 spawnPoint) => spawnPoint;
}
