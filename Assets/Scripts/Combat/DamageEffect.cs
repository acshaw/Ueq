using Mirror;
using UnityEngine;

/// <summary>Runtime-only since M2.9 — built by <see cref="AbilityRegistry"/> from a "damage" row of
/// <c>ability_effects</c>. No longer authored as a sub-asset.</summary>
public class DamageEffect : AbilityEffect
{
    public int             baseDamage    = 10;
    public ScalingStatType scalingStat   = ScalingStatType.None;
    public float           scalingFactor = 0f;

    public override void Apply(NetworkIdentity caster, NetworkIdentity target, AbilityDefinition source)
    {
        var health = target?.GetComponent<Health>();
        if (health == null || health.IsDead) return;

        int damage = baseDamage;
        var stats = caster?.GetComponent<CharacterStats>();
        if (stats != null && scalingStat != ScalingStatType.None)
            damage += Mathf.RoundToInt(GetStat(stats) * scalingFactor);

        health.TakeDamage(Mathf.Max(1, damage), caster);
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
