using System.Collections.Generic;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(NpcEventDispatcher))]
public class NpcConversation : NetworkBehaviour
{
    [SerializeField] ConversationKeywordSet keywordSet;
    [SerializeField] float  hearingRange         = 20f;
    [SerializeField] float  conversationTimeout  = 30f;
    [SerializeField] float  conversationCooldown = 5f;
    [SerializeField] string closingLine          = "Farewell.";

    enum ConvState { Idle, InConversation }

    ConvState      _state;
    NetworkIdentity _partner;
    float           _timeoutTimer;

    readonly Dictionary<NetworkIdentity, HashSet<string>> _unlocked  = new();
    readonly Dictionary<NetworkIdentity, float>           _cooldowns = new();

    NpcEventDispatcher _dispatcher;
    MobApplicator      _mob;

    // M2.4: prefer the DB-backed set (resolved by id from ConversationRegistry); fall back to the
    // serialized field for any non-mob/not-yet-migrated NPC.
    ConversationKeywordSet EffectiveKeywordSet =>
        ConversationRegistry.Get(_mob?.Definition?.conversationSetId)
        ?? keywordSet;

    void Awake()
    {
        _dispatcher = GetComponent<NpcEventDispatcher>();
        _mob        = GetComponent<MobApplicator>();
    }

    void Update()
    {
        if (!isServer) return;

        // Tick cooldowns
        var expired = new List<NetworkIdentity>();
        foreach (var (player, endTime) in _cooldowns)
            if (Time.time >= endTime) expired.Add(player);
        foreach (var p in expired) _cooldowns.Remove(p);

        if (_state != ConvState.InConversation) return;

        // End conversation if the partner un-targets this NPC
        if (!IsPlayerTargetingMe(_partner))
        {
            EndConversation(_partner);
            return;
        }

        _timeoutTimer -= Time.deltaTime;
        if (_timeoutTimer <= 0f) EndConversation(_partner);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // Called by NetworkedPlayer.CmdSendChat on the server
    [Server]
    public void HearMessage(NetworkIdentity player, string message)
    {
        if (!IsPlayerTargetingMe(player)) return;

        var ks = EffectiveKeywordSet;
        if (ks == null) { Debug.LogWarning($"[NpcConversation:{name}] keywordSet is null"); return; }

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > hearingRange) return;

        string lower = message.ToLower().Trim();

        foreach (var kw in ks.Keywords)
        {
            if (!lower.Contains(kw.Keyword.ToLower())) continue;

            if (kw.Mode == KeywordMode.Active)
            {
                if (_state != ConvState.InConversation || _partner != player) continue;
                if (kw.RequiresUnlock && !IsUnlocked(player, kw.Keyword)) continue;
            }

            if (!MeetsFactionRequirement(player, kw)) { Debug.Log($"[NpcConversation:{name}] faction gate blocked '{kw.Keyword}'"); continue; }

            HandleMatch(player, kw);
            return; // first match wins
        }
    }

    // ── State machine ─────────────────────────────────────────────────────────

    [Server]
    void HandleMatch(NetworkIdentity player, ConversationKeyword kw)
    {
        if (kw.IsConversationOpener && _state == ConvState.Idle)
        {
            if (_cooldowns.ContainsKey(player)) return;
            StartConversation(player);
        }

        if (!string.IsNullOrEmpty(kw.Response))
            SayToArea(SubstituteTokens(kw.Response, player));

        foreach (var unlock in kw.UnlocksKeywords)
            Unlock(player, unlock);

        _dispatcher.DispatchConversationKeyword(player, kw.Keyword);

        if (kw.EndsConversation) EndConversation(player);
    }

    [Server]
    void StartConversation(NetworkIdentity player)
    {
        _state        = ConvState.InConversation;
        _partner      = player;
        _timeoutTimer = conversationTimeout;
        _dispatcher.DispatchConversationStart(player);
    }

    [Server]
    void EndConversation(NetworkIdentity player)
    {
        if (!string.IsNullOrEmpty(closingLine)) SayToArea(closingLine);
        _unlocked.Remove(player);
        _cooldowns[player] = Time.time + conversationCooldown;
        _state   = ConvState.Idle;
        _partner = null;
        _dispatcher.DispatchConversationEnd(player);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static bool _warnedUnresolvedFaction;

    bool MeetsFactionRequirement(NetworkIdentity player, ConversationKeyword kw)
    {
        // Resolve the gate's faction: legacy SO ref, else by id (M2.4) via FactionRegistry.
        var faction = kw.RequiredFaction;
        if (faction == null && !string.IsNullOrEmpty(kw.RequiredFactionId))
            faction = FactionRegistry.Get(kw.RequiredFactionId);

        if (faction == null)
        {
            // An id gate that can't resolve yet (factions arrive in DB at 2.6) → treat as ungated.
            if (!string.IsNullOrEmpty(kw.RequiredFactionId) && !_warnedUnresolvedFaction)
            {
                _warnedUnresolvedFaction = true;
                Debug.LogWarning($"[NpcConversation] faction gate '{kw.RequiredFactionId}' unresolved " +
                                 "(factions migrate to DB at 2.6) — treating as ungated for now.");
            }
            return true;
        }
        if (faction.ThresholdTable == null) return true;

        var scores  = player.GetComponent<PlayerFactionScores>();
        int score   = scores != null ? scores.GetEffectiveScore(faction) : 0;
        var table   = faction.ThresholdTable;
        int playerIdx   = table.IndexOf(table.Evaluate(score).Name);
        int requiredIdx = table.IndexOf(kw.RequiredStanding);

        return requiredIdx < 0 || playerIdx >= requiredIdx;
    }

    bool IsUnlocked(NetworkIdentity player, string keyword)
        => _unlocked.TryGetValue(player, out var set) && set.Contains(keyword);

    void Unlock(NetworkIdentity player, string keyword)
    {
        if (!_unlocked.ContainsKey(player)) _unlocked[player] = new HashSet<string>();
        _unlocked[player].Add(keyword);
    }

    string SubstituteTokens(string response, NetworkIdentity player)
    {
        var scores = player.GetComponent<PlayerFactionScores>();
        var exp    = player.GetComponent<PlayerExperience>();
        string cls = exp != null && !string.IsNullOrEmpty(exp.ClassName) ? exp.ClassName : "adventurer";
        response   = response.Replace("<name>",   player.gameObject.name);
        response   = response.Replace("<race>",   scores?.ActualRace ?? "unknown");
        response   = response.Replace("<class>",  cls);                     // 3.1.4 — real class name
        response   = response.Replace("<gender>", exp != null ? exp.Gender.ToString() : "friend"); // 3.1.4
        return response;
    }

    bool IsPlayerTargetingMe(NetworkIdentity player)
    {
        var np = player.GetComponent<NetworkedPlayer>();
        return np != null && np.ServerTarget == netIdentity;
    }

    [Server]
    void SayToArea(string line)
        => ChatManager.Instance?.SendArea(new ChatMessage(ChatChannel.NPC, name, line), transform.position);

    // ── Test hooks ────────────────────────────────────────────────────────────

    [Header("Debug")]
    [SerializeField] string _testMessage;

    [ContextMenu("Test: Say _testMessage")]
    void TestCustomSay() => SimulateSay(_testMessage);

    [ContextMenu("Test: Say 'hail'")]
    void TestHail() => SimulateSay("hail");

    [ContextMenu("Test: Say 'farewell'")]
    void TestFarewell() => SimulateSay("farewell");

    [ContextMenu("Test: Say 'help'")]
    void TestHelp() => SimulateSay("help");

    void SimulateSay(string message)
    {
        NetworkedPlayer player = null;
        foreach (var p in FindObjectsByType<NetworkedPlayer>())
            if (p.isLocalPlayer) { player = p; break; }

        if (player != null) HearMessage(player.GetComponent<NetworkIdentity>(), message);
        else Debug.LogWarning("[NpcConversation] No local NetworkedPlayer found in scene.");
    }
}
