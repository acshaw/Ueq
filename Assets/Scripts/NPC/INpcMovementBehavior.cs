public interface INpcMovementBehavior
{
    void Startup(); // called once when the NPC enters the world (server)
    void Suspend(); // called when combat begins or the NPC dies
    void Resume();  // called when the NPC returns to idle after combat
}
