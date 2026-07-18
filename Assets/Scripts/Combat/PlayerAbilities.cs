using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerAbilities : NetworkBehaviour
{
    public const int  HotbarSize   = 8;
    const        float GcdDuration = 1.5f;

    readonly SyncList<string> _hotbar          = new();
    readonly SyncList<string> _knownAbilities  = new();
    readonly SyncList<float>  _hotbarCooldowns = new();

    float                        _gcdTimer;
    readonly Dictionary<string, float> _linkedTimers     = new();
    readonly List<string>              _linkedTimerKeys  = new();

    PlayerMana     _mana;
    PlayerAnimator _animator;

    public SyncList<string> Hotbar          => _hotbar;
    public SyncList<string> KnownAbilities  => _knownAbilities;
    public SyncList<float>  HotbarCooldowns => _hotbarCooldowns;

    void Awake()
    {
        _mana     = GetComponent<PlayerMana>();
        _animator = GetComponentInChildren<PlayerAnimator>();
    }

    public override void OnStartServer()
    {
        for (int i = 0; i < HotbarSize; i++)
        {
            _hotbar.Add("");
            _hotbarCooldowns.Add(0f);
        }

        // PlayerExperience.OnStartServer fires before us; re-populate now that _hotbar is ready.
        var exp = GetComponent<PlayerExperience>();
        if (exp != null)
            SetRaceClass(exp.CurrentClass);
    }

    // ── Race / class ──────────────────────────────────────────────────────────

    [Server]
    public void SetRaceClass(ClassDefinition cls)
    {
        _knownAbilities.Clear();
        for (int i = 0; i < _hotbar.Count; i++)
            _hotbar[i] = "";

        if (cls == null) return;

        foreach (var abilityId in cls.startingAbilities)
            if (!string.IsNullOrEmpty(abilityId))
                _knownAbilities.Add(abilityId);

        for (int i = 0; i < _knownAbilities.Count && i < _hotbar.Count; i++)
            _hotbar[i] = _knownAbilities[i];
    }

    // ── Persistence (1.3) ───────────────────────────────────────────────────────

    /// <summary>Restore the player's hotbar arrangement from a loaded snapshot. Known abilities are
    /// already reconstructed from the class (SetRaceClass); this only restores slot placement, and
    /// each entry is validated against what the character actually knows.</summary>
    [Server]
    public void LoadHotbar(string[] hotbar)
    {
        if (hotbar == null) return;
        for (int i = 0; i < _hotbar.Count && i < hotbar.Length; i++)
        {
            string id = hotbar[i] ?? "";
            _hotbar[i] = (string.IsNullOrEmpty(id) || _knownAbilities.Contains(id)) ? id : "";
        }
    }

    /// <summary>Export the hotbar arrangement as plain data for a snapshot.</summary>
    public string[] ExportHotbar()
    {
        var arr = new string[_hotbar.Count];
        for (int i = 0; i < _hotbar.Count; i++)
            arr[i] = _hotbar[i] ?? "";
        return arr;
    }

    // ── Hotbar management ─────────────────────────────────────────────────────

    [Server]
    public void SetHotbarSlot(int slot, string abilityId)
    {
        if ((uint)slot >= (uint)HotbarSize) return;
        if (!string.IsNullOrEmpty(abilityId) && !_knownAbilities.Contains(abilityId)) return;
        _hotbar[slot] = abilityId ?? "";
    }

    // ── Casting ───────────────────────────────────────────────────────────────

    [Server]
    public void TryCast(int hotbarSlot, NetworkIdentity target)
    {
        if ((uint)hotbarSlot >= (uint)HotbarSize) return;

        string abilityId = _hotbar[hotbarSlot];
        if (string.IsNullOrEmpty(abilityId)) return;

        var ability = AbilityRegistry.Instance?.Get(abilityId);
        if (ability == null || !_knownAbilities.Contains(abilityId)) return;

        if (IsOnCooldown(ability))
        {
            SendMsg("That ability is not ready yet.");
            return;
        }

        var resolvedTarget = ResolveTarget(ability, target);
        if (resolvedTarget == null)
        {
            SendMsg("Invalid target.");
            return;
        }

        if (ability.targetingType != AbilityTargetType.Self)
        {
            float dist = Vector3.Distance(transform.position, resolvedTarget.transform.position);
            if (dist > ability.range)
            {
                SendMsg("Target is out of range.");
                return;
            }
        }

        if (ability.manaCost > 0)
        {
            if (_mana == null || !_mana.HasMana(ability.manaCost))
            {
                SendMsg("Not enough mana.");
                return;
            }
            _mana.UseMana(ability.manaCost);
        }

        foreach (var effect in ability.effects)
            effect?.Apply(netIdentity, resolvedTarget, ability);

        // Play the ability's animation on every client (ClientRpc → shows for the
        // local caster and remote observers, same pattern as auto-attack).
        if (!string.IsNullOrEmpty(ability.animTrigger))
            RpcPlayAbilityAnim(ability.animTrigger);

        StartCooldown(ability);

        SendMsg($"You cast {ability.displayName}.");

        if (resolvedTarget != netIdentity)
        {
            var targetConn = resolvedTarget.GetComponent<NetworkBehaviour>()?.connectionToClient;
            if (targetConn != null)
                ChatManager.Instance?.SendDirect(
                    new ChatMessage(ChatChannel.Ability, "Ability",
                        $"{gameObject.name} casts {ability.displayName} on you."),
                    targetConn);
        }
    }

    [ClientRpc]
    void RpcPlayAbilityAnim(string trigger)
    {
        // Re-resolve if the model child was (re)built after our initial cache (3.1.4 PlayerModel swap).
        if (_animator == null) _animator = GetComponentInChildren<PlayerAnimator>();
        _animator?.PlayTrigger(trigger);
    }

    NetworkIdentity ResolveTarget(AbilityDefinition ability, NetworkIdentity target)
        => ability.targetingType == AbilityTargetType.Self ? netIdentity : target;

    // ── Cooldown engine ───────────────────────────────────────────────────────

    bool IsOnCooldown(AbilityDefinition ability)
    {
        if (ability.cooldownLinks.Count == 0)
            return _gcdTimer > 0f;

        foreach (var link in ability.cooldownLinks)
        {
            if (link.tag == null) continue;
            if (_linkedTimers.TryGetValue(link.tag.tagId, out float t) && t > 0f)
                return true;
        }
        return false;
    }

    void StartCooldown(AbilityDefinition ability)
    {
        if (ability.cooldownLinks.Count == 0)
        {
            _gcdTimer = GcdDuration;
            return;
        }
        foreach (var link in ability.cooldownLinks)
        {
            if (link.tag == null) continue;
            _linkedTimers[link.tag.tagId] = link.duration;
        }
    }

    void Update()
    {
        if (!isServer) return;

        float dt = Time.deltaTime;

        if (_gcdTimer > 0f)
            _gcdTimer = Mathf.Max(0f, _gcdTimer - dt);

        _linkedTimerKeys.Clear();
        _linkedTimerKeys.AddRange(_linkedTimers.Keys);
        foreach (var key in _linkedTimerKeys)
            _linkedTimers[key] = Mathf.Max(0f, _linkedTimers[key] - dt);

        for (int i = 0; i < HotbarSize; i++)
        {
            string id = i < _hotbar.Count ? _hotbar[i] : "";
            if (string.IsNullOrEmpty(id)) { _hotbarCooldowns[i] = 0f; continue; }
            var ab = AbilityRegistry.Instance?.Get(id);
            _hotbarCooldowns[i] = ab != null ? GetDisplayCooldown(ab) : 0f;
        }
    }

    float GetDisplayCooldown(AbilityDefinition ability)
    {
        if (ability.cooldownLinks.Count == 0)
            return _gcdTimer;

        float max = 0f;
        foreach (var link in ability.cooldownLinks)
        {
            if (link.tag == null) continue;
            if (_linkedTimers.TryGetValue(link.tag.tagId, out float t) && t > max)
                max = t;
        }
        return max;
    }

    void SendMsg(string text) =>
        ChatManager.Instance?.SendDirect(
            new ChatMessage(ChatChannel.Ability, "Ability", text),
            connectionToClient);
}
