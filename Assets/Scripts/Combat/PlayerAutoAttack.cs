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
    [SerializeField] float          _shiftFactor      = 0.15f;

    [SyncVar] bool _on;

    float           _nextAttack;
    NetworkedPlayer _player;
    CharacterStats  _stats;
    PlayerEquipment _equipment;
    PlayerAnimator  _animator;

    void Awake()
    {
        _player    = GetComponent<NetworkedPlayer>();
        _stats     = GetComponent<CharacterStats>();
        _equipment = GetComponent<PlayerEquipment>();
        _animator  = GetComponentInChildren<PlayerAnimator>();
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
        // hit or miss, then resolve damage.
        RpcPlayAttack();

        int damage = ComputeDamage();
        if (damage == 0)
        {
            string targetName = target.GetComponent<Nameplate>()?.Label ?? target.gameObject.name;
            ChatManager.Instance?.SendDirect(
                new ChatMessage(ChatChannel.Combat, "", $"Your attack misses {targetName}."),
                connectionToClient);
        }
        else
        {
            health.TakeDamage(damage, netIdentity);
        }
        _nextAttack = Time.time + EffectiveDelay;
    }

    [ClientRpc]
    void RpcPlayAttack() => _animator?.PlayAttack();

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

    int ComputeDamage()
    {
        int   baseDmg = EffectiveDamage;
        var   cat     = EffectiveCat;

        if (_stats == null) return baseDmg;

        float atk = cat == WeaponCategory.Might
            ? _stats.Str * 0.75f + _stats.Dex * 0.25f
            : _stats.Dex * 0.75f + _stats.Str * 0.25f;

        float relevantStat = cat == WeaponCategory.Might ? _stats.Str : _stats.Dex;

        float hitRoll = Mathf.Clamp(
            Random.Range(0f, 100f) + (atk - _stats.Agi) * _shiftFactor,
            0f, 100f);

        if (hitRoll < 2.5f) return 0; // Miss

        float multiplier, variance;
        if      (hitRoll < 25f) { multiplier = 0.5f;  variance = 0.1f; }   // Glancing
        else if (hitRoll < 75f) { multiplier = 1.0f;  variance = 0.15f; }  // Normal
        else                    { multiplier = 1.5f;  variance = 0.15f; }  // Solid (Critical → Solid until skill unlocked)

        float typeMultiplier = multiplier + Random.Range(-variance, variance);
        float rawDamage = baseDmg * (1f + relevantStat / 400f) * typeMultiplier;
        return Mathf.Max(1, Mathf.RoundToInt(rawDamage));
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
