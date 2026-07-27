using Mirror;
using UnityEngine;

/// <summary>Runtime-only since 5.4 (AG5) — built by AbilityRegistry from a "threat" row of ability_effects.
/// Adds flat threat directly against the target's EnemyAI (no-op on non-mob targets). No scaling, no
/// multiplier system — a flat, generous bonus is enough for Taunt's purpose; a proper threat-multiplier/
/// tank-stance system is explicitly out of scope for this pass (5.4's own scope note).</summary>
public class ThreatEffect : AbilityEffect
{
    public int baseAmount = 50;

    public override void Apply(NetworkIdentity caster, NetworkIdentity target, AbilityDefinition source)
    {
        var ai = target?.GetComponent<EnemyAI>();
        if (ai == null) return;
        ai.AddThreat(caster, baseAmount);
    }
}
