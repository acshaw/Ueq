using Mirror;

public interface IOnSpawned
{
    void OnSpawned();
}

public interface IOnPerceived
{
    void OnPerceived(NetworkIdentity player, float distance);
}

public interface IOnTargeted
{
    void OnTargeted(NetworkIdentity player);
}

public interface IOnConversationKeyword
{
    void OnConversationKeyword(NetworkIdentity player, string keyword);
}

public interface IOnAttacked
{
    void OnAttacked(int damage, NetworkIdentity attacker);
}

public interface IOnFactionChanged
{
    void OnFactionChanged(NetworkIdentity player, int oldScore, int newScore);
}

public interface IOnAggroLost
{
    void OnAggroLost();
}

public interface IOnDeath
{
    void OnDeath(NetworkIdentity attacker);
}

public interface IOnConversationStart
{
    void OnConversationStart(NetworkIdentity player);
}

public interface IOnConversationEnd
{
    void OnConversationEnd(NetworkIdentity player);
}

public interface IOnTimer
{
    void OnTimer();
}
