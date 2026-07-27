using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public enum WeaponCategory { Might, Finesse }

[RequireComponent(typeof(NetworkedPlayer))]
public class PlayerAutoAttack : NetworkBehaviour
{
    [SerializeField] int            _weaponBaseDamage = 10;
    [SerializeField] float          _weaponDelay      = 2f;
    [SerializeField] float          _attackRange      = 3f;
    [SerializeField] WeaponCategory _weaponCategory   = WeaponCategory.Might;

    [SyncVar] bool _on;

    float              _nextAttack;
    NetworkedPlayer    _player;
    CharacterStats     _stats;
    PlayerEquipment    _equipment;
    PlayerAnimator     _animator;
    PlayerWeaponSkills _weaponSkills;

    void Awake()
    {
        _player       = GetComponent<NetworkedPlayer>();
        _stats        = GetComponent<CharacterStats>();
        _equipment    = GetComponent<PlayerEquipment>();
        _animator     = GetComponentInChildren<PlayerAnimator>();
        _weaponSkills = GetComponent<PlayerWeaponSkills>();
    }

    // Read weapon stats from equipped weapon; fall back to serialized inspector values
    ItemDefinition    Weapon          => _equipment?.GetWeapon();
    float             EffectiveDelay  => Weapon?.weaponDelay      ?? _weaponDelay;
    float             EffectiveRange  => Weapon?.weaponRange       ?? _attackRange;
    int               EffectiveDamage => Weapon?.weaponBaseDamage  ?? _weaponBaseDamage;
    WeaponCategory    EffectiveCat    => Weapon != null ? Weapon.weaponCategory : _weaponCategory;

    void Update()
    {
        if (isLocalPlayer && !ChatUI.IsOpen)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.digit1Key.wasPressedThisFrame)
                CmdSetAutoAttack(!_on);
        }

        if (!isServer || !_on) return;

        var target = _player.ServerTarget;
        if (target == null) return;

        var health = target.GetComponent<Health>();
        if (health == null || health.IsDead)
        {
            _on = false;
            ChatManager.Instance?.SendDirect(
                new ChatMessage(ChatChannel.System, "System", "Auto Attack: OFF"),
                connectionToClient);
            return;
        }

        if (Time.time < _nextAttack) return;

        // Proactive no-PvP refusal: mirrors the LOS/range messaging below so a player aiming at another
        // player gets a clear reason instead of silently whiffing forever. Health.TakeDamage already
        // backstops this unconditionally (broadened from same-party-only, 5.3 GP6, to ANY player once
        // players gained a Targetable component); this just makes the failure legible.
        if (target.GetComponent<NetworkedPlayer>() != null)
        {
            _on = false;
            ChatManager.Instance?.SendDirect(
                new ChatMessage(ChatChannel.System, "System", "You cannot attack another player."),
                connectionToClient);
            return;
        }

        if (!HasLineOfSight(target.transform))
        {
            ChatManager.Instance?.SendDirect(
                new ChatMessage(ChatChannel.System, "System", "You cannot see your target."),
                connectionToClient);
            _nextAttack = Time.time + EffectiveDelay;
            return;
        }

        if (Vector3.Distance(transform.position, target.transform.position) > EffectiveRange)
        {
            ChatManager.Instance?.SendDirect(
                new ChatMessage(ChatChannel.System, "System", "Target is out of range."),
                connectionToClient);
            _nextAttack = Time.time + EffectiveDelay;
            return;
        }

        // Swing resolves (in range + LOS) — play the animation on every client,
        // hit or miss, then resolve damage via the combat pipeline (5.1.1-5.1.4).
        RpcPlayAttack();

        var cat = EffectiveCat;
        var ctx = new CombatResolver.AttackContext
        {
            Attacker         = CombatResolver.BuildCombatant(gameObject, cat),
            Defender         = CombatResolver.BuildCombatant(target.gameObject, cat),
            IsRearAttack     = CombatResolver.IsRearAttack(transform, target.transform),
            IsParryable      = true, // player weapon auto-attack is always parryable (AV3)
            WeaponBaseDamage = EffectiveDamage,
            RelevantStat     = cat == WeaponCategory.Might ? (_stats?.Str ?? 0) : (_stats?.Dex ?? 0),
        };
        var result = CombatResolver.ResolveAttack(ctx);
        _weaponSkills?.RollSkillUp(cat); // SK4 — chance to rise on every swing, regardless of outcome

        string targetName = target.GetComponent<Nameplate>()?.Label ?? target.gameObject.name;
        if (result.Tier == HitTier.Miss)
        {
            if (result.Riposted)
            {
                GetComponent<Health>()?.TakeDamage(result.RiposteDamage, target);
                ChatManager.Instance?.SendDirect(
                    new ChatMessage(ChatChannel.Combat, "", $"{targetName} ripostes your attack!"),
                    connectionToClient);
            }
            else
            {
                ChatManager.Instance?.SendDirect(
                    new ChatMessage(ChatChannel.Combat, "", $"Your attack misses {targetName}."),
                    connectionToClient);
            }
        }
        else
        {
            health.TakeDamage(result.Damage, netIdentity);
        }
        _nextAttack = Time.time + EffectiveDelay;
    }

    [ClientRpc]
    void RpcPlayAttack()
    {
        // Re-resolve if the model child was (re)built after our initial cache (3.1.4 PlayerModel swap).
        if (_animator == null) _animator = GetComponentInChildren<PlayerAnimator>();
        _animator?.PlayAttack();
    }

    [Command]
    void CmdSetAutoAttack(bool on)
    {
        _on = on;
        string msg;
        if (on)
        {
            msg = _player.ServerTarget == null
                ? "Auto Attack: ON - You have no target."
                : "Auto Attack: ON";
        }
        else
        {
            msg = "Auto Attack: OFF";
        }
        ChatManager.Instance?.SendDirect(
            new ChatMessage(ChatChannel.System, "System", msg), connectionToClient);
    }

    bool HasLineOfSight(Transform target)
    {
        Vector3 toTarget = (target.position - transform.position).normalized;
        if (Vector3.Dot(transform.forward, toTarget) <= 0f)
            return false;

        Vector3 from = transform.position + Vector3.up * 1.5f;
        Vector3 to   = target.position    + Vector3.up * 1.0f;
        if (Physics.Linecast(from, to, out RaycastHit hit,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.transform.IsChildOf(target) || hit.transform == target;
        return true;
    }

    public bool IsOn => _on;
}
