using UnityEngine;

/// <summary>
/// The Eventide combat pipeline (docs/eventide_combat_pipeline.md): Hit Roll (5.1.1) → Avoidance (5.1.2)
/// → Damage (5.1.3) → Mitigation (5.1.4, stub). One static entry point, <see cref="ResolveAttack"/>,
/// used by both <c>PlayerAutoAttack</c> and <c>EnemyAI</c> — 5.1's "symmetric" scope decision means the
/// same code resolves player-vs-mob and mob-vs-player swings. Pure aside from <see cref="Random"/>.
/// </summary>
public static class CombatResolver
{
    // ── Tunable constants — named per the source doc's own "tuning parameter, adjust through
    // playtesting" framing. Not magic numbers hidden inline. ──────────────────────────────────────────

    // HR3 — Level Differential (design doc §2.7). Fibonacci band floors + how many level-increments of
    // gap it takes to reach "full futility" once the lower-level combatant sits in that band.
    static readonly int[] LevelBandFloor      = { 1, 2, 3, 5, 8, 13 };
    static readonly int[] LevelBandIncrements = { 1, 1, 2, 3, 5, 8 };
    const float MaxLevelFutilityWeight = 100f; // weight units transferred at "full futility"
    const float LevelAdvantageScale    = 0.5f; // disadvantage hurts faster than advantage helps (§2.7)

    // HR6 / §2.9 — Position Modifier: rear attack pulls this many weight units into SolidHit only.
    const float RearAttackWeight = 50f;

    // AV4 — Riposte counter-attack damage, relative to a normal Solid-Hit-tier result (doc: TBD %).
    const float RiposteDamageMultiplier = 0.5f;

    // 5.1.5 (AD1) — §2.10 Stat Contribution: EffectiveSkill = trained weapon skill + relevant stat × ratio.
    const float StatToSkillRatio = 0.1f;

    // 2026-08-21 — Damage step rework: RelevantStat converts to a percentage (STR/DEX × 0.01, no cap —
    // itemization determines the practical ceiling) applied to WeaponBonusDamage (a weapon's
    // stat-scalable portion), then added to WeaponBaseDamage (its flat portion). Replaces the old
    // WeaponBaseDamage × (1 + RelevantStat/400) term, which scaled a weapon's entire damage instead of
    // letting itemization decide how much of it is flat vs. stat-scaled.
    const float StatToDamagePercentRatio = 0.01f;

    // 5.1.5 (AD4/AD9) — shared ATK band the Hit Roll base table is interpolated across (MinAtk →
    // WarriorLevel1, MaxAtk → WarriorLevel20). Placeholder, tuned via the Combat Simulator, not locked
    // by the design doc.
    const float MinAtk = 10f;
    const float MaxAtk = 150f;

    // ── Public shapes ────────────────────────────────────────────────────────────────────────────────

    public struct Combatant
    {
        public float Atk;   // 5.1.5 — replaces BaseTable + Skill; the base table is now derived from this
        public int   Level;

        // 2026-08-13 follow-up — three independent, fully-resolved avoidance values (already includes
        // AvoidanceBase for Dodge; Parry/Riposte stand alone — see BuildCombatant). Replaces the old
        // Agility/Dexterity pair, which fed Dodge and Riposte+Parry (identically) off two raw stats.
        public float Dodge;
        public float Parry;
        public float Riposte;

        // 2026-08-21 (Mitigation) — AC, this combatant's sole mitigation lever (MitigationCurve).
        public float Ac;
    }

    public struct AttackContext
    {
        public Combatant Attacker;
        public Combatant Defender;
        public bool  IsRearAttack;
        public bool  IsParryable;
        public float WeaponBaseDamage;
        public float WeaponBonusDamage; // stat-scalable portion of the weapon's damage (2026-08-21)
        public float RelevantStat; // attacker STR (Might weapon) or DEX (Finesse weapon); 0 for mobs
    }

    public struct AttackResult
    {
        public HitTier Tier;          // final tier after avoidance (Miss if avoided)
        public int     Damage;        // 0 on Miss
        public bool    Riposted;
        public int     RiposteDamage; // counter-attack damage against the original attacker, if Riposted
    }

    // ── Combatant construction (shared by both attacker and defender roles) ────────────────────────────

    /// <summary>
    /// Builds a <see cref="Combatant"/> from whichever side owns <paramref name="go"/> — a mob (reads its
    /// authored <see cref="MobDefinition"/> ATK/Dodge/Parry/Riposte directly, AD3/AV3) or a player
    /// (derives ATK from trained weapon skill + the relevant stat + trained Offense, and Dodge/Parry/
    /// Riposte from Agility + trained Defense/Dodge/Parry/Riposte, AD1/AD2). <paramref name="category"/>
    /// is the weapon category of the swing being resolved — used to look up this combatant's skill/stat
    /// in that category, whether they're attacking or defending (the defender's resulting Atk goes
    /// unused downstream since 5.1.5 retired the Skill Differential step, but is still computed here to
    /// keep this method symmetric for both roles).
    /// </summary>
    public static Combatant BuildCombatant(GameObject go, WeaponCategory category)
    {
        var mobApp = go.GetComponent<MobApplicator>();
        if (mobApp != null && mobApp.Definition != null)
        {
            var def = mobApp.Definition;
            return new Combatant
            {
                Atk     = def.atk, // AD3 — authored directly, no EffectiveSkill/Offense split
                Level   = def.mobLevel,
                // AV3 (2026-08-13 follow-up) — mobs author all three avoidance checks directly as flat
                // numbers, no formula, same reasoning as ATK: mobs have nothing to derive them from.
                Dodge   = def.avoidanceDodge,
                Parry   = def.avoidanceParry,
                Riposte = def.avoidanceRiposte,
                Ac      = def.ac,
            };
        }

        var exp       = go.GetComponent<PlayerExperience>();
        var pws       = go.GetComponent<PlayerWeaponSkills>();
        var offense   = go.GetComponent<PlayerOffense>();
        var avoidance = go.GetComponent<PlayerAvoidanceSkills>();
        var stats     = go.GetComponent<CharacterStats>();
        int level = exp != null ? exp.Level : 1;
        var cls   = exp != null ? exp.CurrentClass : null;

        // 5.1.5 (AD1/AD2): ATK = EffectiveSkill (trained skill + relevant stat × ratio, §2.10) +
        // trained Offense (a persisted stat like WeaponSkill, follow-up 2026-08-13 — no longer a fixed
        // level×OffensePerLevel formula; see PlayerOffense.cs).
        //
        // Avoidance (2026-08-13 follow-up, replaces the 2026-08-11 EffectiveDefense design):
        //   AvoidanceBase = Agility × StatToSkillRatio + Defense    (Defense: trained, PlayerAvoidanceSkills)
        //   EffectiveDodge   = AvoidanceBase + Dodge                 (works even with Dodge untrained — an
        //                                                             innate/reflexive check)
        //   EffectiveParry   = Parry                                 (stands alone — genuinely ~0% until
        //   EffectiveRiposte = Riposte                                trained; no AvoidanceBase contribution)
        // Dexterity no longer feeds Avoidance at all — it's purely offensive now (ATK/Damage for a
        // Finesse weapon). No class assigned (e.g. pre-4.x testing) falls back to MinAtk / raw Agility
        // for Dodge only, Parry/Riposte = 0 (nothing trained yet).
        float atk, dodge, parry, riposte;
        if (cls != null)
        {
            int   weaponSkill    = pws != null ? pws.For(category) : 0;
            float relevantStat   = stats != null ? (category == WeaponCategory.Might ? stats.Str : stats.Dex) : 0f;
            float effectiveSkill = weaponSkill + relevantStat * StatToSkillRatio;
            int   offenseValue   = offense != null ? offense.Value : 0;
            atk = effectiveSkill + offenseValue;

            float statAgi        = stats != null ? stats.Agi : 0f;
            int   defenseValue   = avoidance != null ? avoidance.Defense : 0;
            float avoidanceBase  = statAgi * StatToSkillRatio + defenseValue;
            dodge   = avoidanceBase + (avoidance != null ? avoidance.Dodge : 0);
            parry   = avoidance != null ? avoidance.Parry   : 0;
            riposte = avoidance != null ? avoidance.Riposte : 0;
        }
        else
        {
            atk     = MinAtk;
            dodge   = stats != null ? stats.Agi : 0f;
            parry   = 0f;
            riposte = 0f;
        }

        // 2026-08-21 (Mitigation) — AC has no class/race base, unlike Dodge's AvoidanceBase; it's purely
        // the sum of equipped gear (CharacterStats.Ac), independent of the cls != null branch above.
        float ac = stats != null ? stats.Ac : 0f;

        return new Combatant { Atk = atk, Level = level, Dodge = dodge, Parry = parry, Riposte = riposte, Ac = ac };
    }

    // 5.1.5 (AD4) — re-keys the (unchanged) table lerp from "by level" to "by ATK".
    static CombatTierTable ResolveAtkTable(float atk)
    {
        float fraction = Mathf.Clamp01((atk - MinAtk) / (MaxAtk - MinAtk));
        return CombatTierTable.Lerp(CombatTierTable.WarriorLevel1, CombatTierTable.WarriorLevel20, fraction);
    }

    public static bool IsRearAttack(Transform attacker, Transform defender)
    {
        Vector3 toAttacker = attacker.position - defender.position;
        toAttacker.y = 0f;
        if (toAttacker.sqrMagnitude < 0.0001f) return false;
        return Vector3.Dot(defender.forward, toAttacker.normalized) < 0f;
    }

    // ── Pipeline entry point ─────────────────────────────────────────────────────────────────────────

    public static AttackResult ResolveAttack(AttackContext ctx)
    {
        // Step 1 — Hit Roll
        var table = ResolveAtkTable(ctx.Attacker.Atk);
        ApplyLevelDifferential(ref table, ctx.Attacker.Level, ctx.Defender.Level);
        if (ctx.IsRearAttack) ApplyPositionModifier(ref table);
        HitTier tier = WeightedPick(table);

        // Step 2 — Avoidance
        bool riposted = false;
        if (tier != HitTier.Miss)
        {
            var (avoided, didRiposte) = ResolveAvoidance(ctx.Defender, ctx.IsParryable);
            if (avoided)
            {
                tier     = HitTier.Miss;
                riposted = didRiposte;
            }
        }

        // Step 3 — Damage
        int damage = tier == HitTier.Miss ? 0 : ComputeDamage(tier, ctx.WeaponBaseDamage, ctx.WeaponBonusDamage, ctx.RelevantStat);

        // Step 4 — Mitigation (5.1.4, stub — no-op seam)
        damage = ApplyMitigation(damage, ctx.Defender);

        int riposteDamage = 0;
        if (riposted)
        {
            // AV4: the counter attack uses Step 3 only — bypasses Step 2 (already resolved) and Step 4
            // (no mitigation), at a reduced multiplier relative to a normal Solid Hit.
            riposteDamage = ComputeDamage(HitTier.SolidHit, ctx.WeaponBaseDamage, ctx.WeaponBonusDamage, ctx.RelevantStat, RiposteDamageMultiplier);
        }

        return new AttackResult { Tier = tier, Damage = damage, Riposted = riposted, RiposteDamage = riposteDamage };
    }

    // ── Step 1 — Hit Roll helpers ────────────────────────────────────────────────────────────────────

    static void ApplyLevelDifferential(ref CombatTierTable table, int attackerLevel, int defenderLevel)
    {
        int gap = attackerLevel - defenderLevel;
        if (gap == 0) return;

        int lowLevel     = Mathf.Min(attackerLevel, defenderLevel);
        int increments   = IncrementsToFutility(lowLevel);
        float fraction   = Mathf.Clamp01(Mathf.Abs(gap) / (float)increments);

        if (gap < 0) // attacker at a level disadvantage — hurts at full magnitude
            ShiftTowardFutility(ref table, fraction * MaxLevelFutilityWeight);
        else         // attacker at a level advantage — helps at reduced magnitude
            ShiftTowardPotency(ref table, fraction * MaxLevelFutilityWeight * LevelAdvantageScale);
    }

    static int IncrementsToFutility(int lowLevel)
    {
        for (int i = LevelBandFloor.Length - 1; i >= 0; i--)
            if (lowLevel >= LevelBandFloor[i]) return LevelBandIncrements[i];
        return LevelBandIncrements[0];
    }

    static void ApplyPositionModifier(ref CombatTierTable table)
    {
        // §2.9: rear attack pulls weight from Miss/Glancing/Hit into SolidHit only — reliability, not
        // explosiveness. Drain proportionally from the three low tiers, floor at zero.
        float pool = table.Miss + table.Glancing + table.Hit;
        if (pool <= 0f) return;
        float amount = Mathf.Min(RearAttackWeight, pool);

        float takeMiss     = amount * (table.Miss     / pool);
        float takeGlancing = amount * (table.Glancing / pool);
        float takeHit      = amount * (table.Hit      / pool);

        table.Miss     -= takeMiss;
        table.Glancing -= takeGlancing;
        table.Hit      -= takeHit;
        table.SolidHit += takeMiss + takeGlancing + takeHit;
    }

    // Shared directional-weight-transfer helpers — push the table "toward futility" or "toward potency"
    // by a given weight (currently only Level Differential uses these; Skill Differential was retired in
    // 5.1.5 since weapon skill's influence now flows through ATK instead). Crippling never receives
    // weight here — it's class-passive-unlock-only (§2.3/§2.11) and no passive system exists yet; the
    // doc itself says pre-unlock those points redistribute into Critical instead.
    static readonly HitTier[] FutilityDrainOrder = { HitTier.Crippling, HitTier.Critical, HitTier.GoodHit, HitTier.SolidHit, HitTier.Hit };
    static readonly HitTier[] FutilityFillOrder  = { HitTier.Miss, HitTier.Glancing };
    static readonly HitTier[] PotencyDrainOrder  = { HitTier.Miss, HitTier.Glancing, HitTier.Hit };
    static readonly HitTier[] PotencyFillOrder   = { HitTier.SolidHit, HitTier.GoodHit, HitTier.Critical };

    static void ShiftTowardFutility(ref CombatTierTable table, float amount) => Transfer(ref table, amount, FutilityDrainOrder, FutilityFillOrder);
    static void ShiftTowardPotency(ref CombatTierTable table, float amount)  => Transfer(ref table, amount, PotencyDrainOrder, PotencyFillOrder);

    static void Transfer(ref CombatTierTable table, float amount, HitTier[] drainFrom, HitTier[] fillInto)
    {
        if (amount <= 0f) return;

        float available = 0f;
        foreach (var t in drainFrom) available += table.Get(t);
        float moved = Mathf.Min(amount, available);
        if (moved <= 0f) return;

        foreach (var t in drainFrom)
        {
            float have = table.Get(t);
            if (have <= 0f) continue;
            float take = moved * (have / available);
            table.Add(t, -take);
        }

        float each = moved / fillInto.Length;
        foreach (var t in fillInto)
            table.Add(t, each);
    }

    static HitTier WeightedPick(CombatTierTable table)
    {
        float total = table.Total;
        if (total <= 0f) return HitTier.Miss;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        foreach (var t in CombatTierTable.Order)
        {
            cumulative += table.Get(t);
            if (roll < cumulative) return t;
        }
        return HitTier.Crippling; // float rounding fallback
    }

    // ── Step 2 — Avoidance (5.1.2) ───────────────────────────────────────────────────────────────────

    static (bool avoided, bool riposted) ResolveAvoidance(Combatant defender, bool isParryable)
    {
        // Riposte — its own independently-trained value (2026-08-13 follow-up), no longer sharing
        // Parry's number.
        if (Random.Range(0f, 100f) < AvoidanceCurve.Evaluate(defender.Riposte))
            return (true, true);

        // Parry — skipped for non-parryable attacks (beast bites, etc., AV3), falls through to Dodge.
        if (isParryable && Random.Range(0f, 100f) < AvoidanceCurve.Evaluate(defender.Parry))
            return (true, false);

        // Dodge — includes AvoidanceBase (Agility + Defense), so this is nonzero even untrained.
        if (Random.Range(0f, 100f) < AvoidanceCurve.Evaluate(defender.Dodge))
            return (true, false);

        return (false, false);
    }

    // ── Step 3 — Damage (5.1.3) ──────────────────────────────────────────────────────────────────────

    static int ComputeDamage(HitTier tier, float weaponBaseDamage, float weaponBonusDamage, float relevantStat, float extraMultiplier = 1f)
    {
        if (tier == HitTier.Miss) return 0;

        var config = CombatTierDamageConfig.Active;
        float pct      = config.PercentFor(tier);
        float variance = 1f + Random.Range(-config.variance, config.variance);

        float baseDmg = relevantStat * StatToDamagePercentRatio * weaponBonusDamage + weaponBaseDamage;
        float raw     = baseDmg * pct * variance * extraMultiplier;
        return Mathf.Max(1, Mathf.RoundToInt(raw));
    }

    // ── Step 4 — Mitigation (2026-08-21) ─────────────────────────────────────────────────────────────

    // MT1 — AC is the sole lever, converted to a % via MitigationCurve's diminishing-returns curve.
    // A Miss (rawDamage 0) passes through unchanged; any landed hit still deals at least 1, same floor
    // Step 3 already enforces (mitigation cannot create invulnerability, per the design doc's §5 notes).
    static int ApplyMitigation(int rawDamage, Combatant defender)
    {
        if (rawDamage <= 0) return rawDamage;
        float pct = MitigationCurve.Evaluate(defender.Ac);
        return Mathf.Max(1, Mathf.RoundToInt(rawDamage * (1f - pct / 100f)));
    }
}
