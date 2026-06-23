using Mirror;
using UnityEngine;

[CreateAssetMenu(menuName = "Ueq/Ability Effects/Heal")]
public class HealEffect : AbilityEffect
{
    public int             baseHeal      = 20;
    public ScalingStatType scalingStat   = ScalingStatType.None;
    public float           scalingFactor = 0f;

    public override void Apply(NetworkIdentity caster, NetworkIdentity target, AbilityDefinition source)
    {
        var health = target?.GetComponent<Health>();
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
