using Mirror;
using UnityEngine;

/// <summary>Runtime-only since M2.9 — built by <see cref="AbilityRegistry"/> from a "heal" row of
/// <c>ability_effects</c>. No longer authored as a sub-asset.</summary>
public class HealEffect : AbilityEffect
{
    public int             baseHeal      = 20;
    public ScalingStatType scalingStat   = ScalingStatType.None;
    public float           scalingFactor = 0f;

    public override void Apply(NetworkIdentity caster, NetworkIdentity target, AbilityDefinition source)
    {
        // Players only — Health.Heal() itself has no restriction, so without this an enemy mob targeted
        // via click-targeting could be "healed" (an odd, exploit-adjacent edge case with no legitimate use).
        if (target == null || target.GetComponent<NetworkedPlayer>() == null) return;

        var health = target.GetComponent<Health>();
        if (health == null || health.IsDead) return;

        int amount = baseHeal;
        var stats = caster?.GetComponent<CharacterStats>();
        if (stats != null && scalingStat != ScalingStatType.None)
            amount += Mathf.RoundToInt(GetStat(stats) * scalingFactor);

        health.Heal(Mathf.Max(1, amount));
    }

    int GetStat(CharacterStats s) => scalingStat switch
    {
        ScalingStatType.Str => s.Str,
        ScalingStatType.Sta => s.Sta,
        ScalingStatType.Agi => s.Agi,
        ScalingStatType.Dex => s.Dex,
        ScalingStatType.Int => s.Int,
        ScalingStatType.Wis => s.Wis,
        ScalingStatType.Cha => s.Cha,
        _                   => 0,
    };
}
