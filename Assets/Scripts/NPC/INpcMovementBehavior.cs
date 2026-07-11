using UnityEngine;

public interface INpcMovementBehavior
{
    void Startup(); // called once when the NPC enters the world (server)
    void Suspend(); // called when combat begins or the NPC dies
    void Resume();  // called when the NPC returns to idle after combat

    // 3.1.11 (WR5) — where the NPC should head when it disengages. Spawn-leashed wander / patrol / stationary
    // return the spawn point (walk home + heal); free-range/bounded wander returns the current position (reset
    // in place, so a roamer doesn't trudge back across its whole territory after every kill).
    Vector3 GetReturnAnchor(Vector3 spawnPoint);
}
