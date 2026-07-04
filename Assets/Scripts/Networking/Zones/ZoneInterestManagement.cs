using Mirror;

/// <summary>
/// M3.0 (Stage B) — project subclass of Mirror's <see cref="SceneInterestManagement"/> that partitions
/// network visibility by scene (so each zone is isolated). The only change is a null guard:
///
/// With <c>autoCreatePlayer = false</c> (our character-select flow, 1.5) a connection is "ready" while it
/// sits at the select screen with **no** <c>conn.identity</c>. Mirror's
/// <c>NetworkServer.SpawnObserversForConnection</c> still calls <c>OnCheckObserver(identity, conn)</c> for
/// that connection, and the base implementation dereferences <c>newObserver.identity.gameObject.scene</c>
/// → NullReferenceException. A player-less connection should observe nothing, so we short-circuit to false.
/// </summary>
public class ZoneInterestManagement : SceneInterestManagement
{
    public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
        => newObserver != null && newObserver.identity != null && base.OnCheckObserver(identity, newObserver);
}
