using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 5.3 — server-only canonical party registry (GP1: session-only, no persistence — parties dissolve on
/// disconnect). Mirrors the ZoneManager singleton convention: a plain MonoBehaviour (no NetworkIdentity of
/// its own — pure server orchestration; all client-visible state rides on each player's PlayerParty
/// SyncList/SyncVars). Lives on the NetworkManager GameObject; driven by GameNetworkManager
/// (ServerInitialize / ServerShutdown / HandleDisconnect).
/// </summary>
public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }

    public const int MaxMembers = 6;
    const float InviteTimeoutSeconds = 60f;

    class Party
    {
        public uint Id;
        public NetworkIdentity Leader;
        public readonly List<NetworkIdentity> Members = new();
    }

    class Invite
    {
        public NetworkIdentity Inviter;
        public float ExpiresAt;
    }

    uint _nextPartyId = 1;
    readonly Dictionary<uint, Party> _parties = new();
    readonly Dictionary<NetworkIdentity, uint> _partyOf = new();          // member -> party id
    readonly Dictionary<NetworkIdentity, Invite> _invites = new();        // invited player -> pending invite
    readonly List<NetworkIdentity> _expiredScratch = new();               // Update() scratch, no per-tick alloc

    // ── Lifecycle (called by GameNetworkManager) ─────────────────────────────────

    public void ServerInitialize() => Instance = this;

    public void ServerShutdown()
    {
        _parties.Clear();
        _partyOf.Clear();
        _invites.Clear();
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!NetworkServer.active || _invites.Count == 0) return;

        _expiredScratch.Clear();
        foreach (var (target, inv) in _invites)
            if (Time.time > inv.ExpiresAt) _expiredScratch.Add(target);

        foreach (var t in _expiredScratch) _invites.Remove(t);
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Defensive copy of a party's member list. Used by EnemyAI.ResolveCreditedGroup (5.3 GP5).</summary>
    [Server]
    public List<NetworkIdentity> MembersOf(uint partyId)
        => _parties.TryGetValue(partyId, out var p) ? new List<NetworkIdentity>(p.Members) : new List<NetworkIdentity>();

    // ── Commands ──────────────────────────────────────────────────────────────

    [Server]
    public void Invite(NetworkIdentity inviter, string targetName)
    {
        var invParty = inviter?.GetComponent<PlayerParty>();
        if (invParty == null) return;

        // Only the leader may invite (GP3). A solo player (no party yet) implicitly becomes leader of a
        // brand-new one the moment their first invite is accepted.
        if (invParty.InParty && !invParty.IsLeader)
        { Msg(inviter, "Only the party leader can invite."); return; }

        var targetConn = ChatManager.Instance?.FindConnectionByName(targetName);
        var target = targetConn?.identity;
        if (target == null) { Msg(inviter, $"Player '{targetName}' not found."); return; }
        if (target == inviter) { Msg(inviter, "You can't invite yourself."); return; }

        int currentSize = invParty.InParty ? MembersOf(invParty.PartyId).Count : 1;
        if (currentSize >= MaxMembers) { Msg(inviter, "Your party is full."); return; }

        var targetParty = target.GetComponent<PlayerParty>();
        if (targetParty != null && targetParty.InParty)
        { Msg(inviter, $"{targetName} is already in a group."); return; }

        _invites[target] = new Invite { Inviter = inviter, ExpiresAt = Time.time + InviteTimeoutSeconds };

        Msg(inviter, $"You invite {targetName} to your group.");
        Msg(target, $"{inviter.name} invited you to a group. Type /accept to join.");
    }

    [Server]
    public void AcceptInvite(NetworkIdentity player)
    {
        if (player == null) return;
        if (!_invites.TryGetValue(player, out var invite) || Time.time > invite.ExpiresAt)
        { Msg(player, "You have no pending invite."); return; }

        _invites.Remove(player);

        var inviterParty = invite.Inviter?.GetComponent<PlayerParty>();
        if (invite.Inviter == null || inviterParty == null)
        { Msg(player, "That invite is no longer valid."); return; }

        // Form a new party if the inviter wasn't already leading one.
        uint partyId = inviterParty.PartyId;
        if (partyId == 0)
        {
            partyId = _nextPartyId++;
            var party = new Party { Id = partyId, Leader = invite.Inviter };
            party.Members.Add(invite.Inviter);
            _parties[partyId] = party;
            _partyOf[invite.Inviter] = partyId;
        }

        if (!_parties.TryGetValue(partyId, out var p)) return;
        if (p.Members.Count >= MaxMembers) { Msg(player, "That party is now full."); return; }

        p.Members.Add(player);
        _partyOf[player] = partyId;

        BroadcastRoster(p);
        Msg(player, "You join the group.");
        foreach (var m in p.Members)
            if (m != player) Msg(m, $"{player.name} has joined the group.");
    }

    [Server]
    public void Leave(NetworkIdentity player)
    {
        if (player == null || !_partyOf.TryGetValue(player, out var partyId)) return;
        RemoveMember(partyId, player, wasKicked: false);
    }

    [Server]
    public void Disband(NetworkIdentity player)
    {
        var party = PartyOf(player, out uint partyId);
        if (party == null) return;
        if (party.Leader != player) { Msg(player, "Only the party leader can disband the group."); return; }

        var members = new List<NetworkIdentity>(party.Members);
        _parties.Remove(partyId);
        foreach (var m in members)
        {
            _partyOf.Remove(m);
            m?.GetComponent<PlayerParty>()?.ServerSyncRoster(0, null, null);
            Msg(m, "The group has been disbanded.");
        }
    }

    [Server]
    public void Kick(NetworkIdentity leader, string targetName)
    {
        var party = PartyOf(leader, out uint partyId);
        if (party == null) return;
        if (party.Leader != leader) { Msg(leader, "Only the party leader can kick."); return; }

        var target = ResolveMember(party, targetName);
        if (target == null) { Msg(leader, $"'{targetName}' is not in your group."); return; }
        if (target == leader) { Msg(leader, "You can't kick yourself — use /disband or /leave."); return; }

        RemoveMember(partyId, target, wasKicked: true);
    }

    [Server]
    public void MakeLeader(NetworkIdentity leader, string targetName)
    {
        var party = PartyOf(leader, out uint partyId);
        if (party == null) return;
        if (party.Leader != leader) { Msg(leader, "Only the party leader can transfer leadership."); return; }

        var target = ResolveMember(party, targetName);
        if (target == null) { Msg(leader, $"'{targetName}' is not in your group."); return; }
        if (target == leader) { Msg(leader, "You are already the leader."); return; }

        party.Leader = target;
        BroadcastRoster(party);
        foreach (var m in party.Members)
            Msg(m, $"{target.name} is now the party leader.");
    }

    [Server]
    public void SendGroupChat(NetworkIdentity sender, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var party = PartyOf(sender, out _);
        if (party == null) { Msg(sender, "You are not in a group."); return; }

        // Zone-independent delivery (GP10) — same conn.Send mechanism as every other chat channel, so a
        // split party still hears each other regardless of SceneInterestManagement partitioning.
        var msg = new ChatMessage(ChatChannel.Group, sender.name, text);
        foreach (var m in party.Members)
        {
            var conn = m?.connectionToClient;
            if (conn != null) ChatManager.Instance?.SendDirect(msg, conn);
        }
    }

    /// <summary>Treat a disconnect the same as /leave (GP1 session-only + GP3).</summary>
    [Server]
    public void HandleDisconnect(NetworkIdentity player)
    {
        if (player == null || !_partyOf.ContainsKey(player)) return;
        Leave(player);
    }

    // ── Internals ────────────────────────────────────────────────────────────

    Party PartyOf(NetworkIdentity player, out uint partyId)
    {
        partyId = 0;
        if (player == null || !_partyOf.TryGetValue(player, out partyId)) return null;
        return _parties.TryGetValue(partyId, out var p) ? p : null;
    }

    static NetworkIdentity ResolveMember(Party party, string name)
    {
        foreach (var m in party.Members)
            if (m != null && m.name == name) return m;
        return null;
    }

    void RemoveMember(uint partyId, NetworkIdentity player, bool wasKicked)
    {
        if (!_parties.TryGetValue(partyId, out var party)) return;

        party.Members.Remove(player);
        _partyOf.Remove(player);
        player?.GetComponent<PlayerParty>()?.ServerSyncRoster(0, null, null);
        Msg(player, wasKicked ? "You have been removed from the group." : "You have left the group.");

        if (party.Members.Count <= 1)
        {
            // Solo remainder auto-disbands (GP3) — no group of one.
            _parties.Remove(partyId);
            if (party.Members.Count == 1)
            {
                var last = party.Members[0];
                _partyOf.Remove(last);
                last?.GetComponent<PlayerParty>()?.ServerSyncRoster(0, null, null);
                Msg(last, "Your group has disbanded (last member remaining).");
            }
            return;
        }

        // Promote the longest-standing remaining member if the leader left/was kicked (GP3) — Members
        // preserves join order, so index 0 is always that member.
        if (party.Leader == player)
        {
            party.Leader = party.Members[0];
            foreach (var m in party.Members)
                Msg(m, $"{party.Leader.name} is now the party leader.");
        }

        string verb = wasKicked ? "was removed from the group" : "has left the group";
        foreach (var m in party.Members)
            if (m != player) Msg(m, $"{player.name} {verb}.");

        BroadcastRoster(party);
    }

    void BroadcastRoster(Party party)
    {
        foreach (var m in party.Members)
            m?.GetComponent<PlayerParty>()?.ServerSyncRoster(party.Id, party.Leader, party.Members);
    }

    static void Msg(NetworkIdentity player, string text)
    {
        var conn = player?.connectionToClient;
        if (conn != null)
            ChatManager.Instance?.SendDirect(new ChatMessage(ChatChannel.System, "System", text), conn);
    }
}
