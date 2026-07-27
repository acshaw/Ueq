using System.Collections.Generic;
using Mirror;

/// <summary>
/// 5.3 — per-player party membership + commands. Mirrors PlayerSitting's pattern: this component owns its
/// own [Command] methods, invoked from ChatUI via LocalPlayer.Current.GetComponent&lt;PlayerParty&gt;().
/// Canonical party state lives in the server-only PartyManager singleton (session-only, GP1 — no
/// persistence, parties dissolve on disconnect); this SyncList/SyncVar pair is pushed a mirror of the
/// current party by PartyManager on every change (GP2), so clients (group frames, F1-F6 targeting) have a
/// live synced roster with no round-trip needed to read it.
/// </summary>
public class PlayerParty : NetworkBehaviour
{
    [SyncVar] uint _partyId; // 0 = not in a party
    [SyncVar] NetworkIdentity _leader;

    readonly SyncList<NetworkIdentity> _members = new();

    public uint PartyId => _partyId;
    public NetworkIdentity Leader => _leader;
    public bool IsLeader => netIdentity != null && _leader == netIdentity;
    public bool InParty => _partyId != 0;
    public SyncList<NetworkIdentity> Members => _members;

    /// <summary>5.3 (GP6) — true if both identities are players in the same non-zero party. Static so
    /// Health/PlayerAutoAttack can check it without a PartyManager round-trip.</summary>
    public static bool InSameParty(NetworkIdentity a, NetworkIdentity b)
    {
        if (a == null || b == null) return false;
        var pa = a.GetComponent<PlayerParty>();
        var pb = b.GetComponent<PlayerParty>();
        return pa != null && pb != null && pa._partyId != 0 && pa._partyId == pb._partyId;
    }

    /// <summary>Called by PartyManager on every affected member whenever the roster changes.</summary>
    [Server]
    public void ServerSyncRoster(uint partyId, NetworkIdentity leader, List<NetworkIdentity> members)
    {
        _partyId = partyId;
        _leader  = leader;
        _members.Clear();
        if (members != null)
            foreach (var m in members) _members.Add(m);
    }

    // ── Commands (client → server) ────────────────────────────────────────────

    [Command] public void CmdInvite(string targetName)   => PartyManager.Instance?.Invite(netIdentity, targetName);
    [Command] public void CmdAccept()                     => PartyManager.Instance?.AcceptInvite(netIdentity);
    [Command] public void CmdLeave()                       => PartyManager.Instance?.Leave(netIdentity);
    [Command] public void CmdDisband()                     => PartyManager.Instance?.Disband(netIdentity);
    [Command] public void CmdKick(string targetName)      => PartyManager.Instance?.Kick(netIdentity, targetName);
    [Command] public void CmdMakeLeader(string targetName) => PartyManager.Instance?.MakeLeader(netIdentity, targetName);
    [Command] public void CmdGroupChat(string text)       => PartyManager.Instance?.SendGroupChat(netIdentity, text);
}
