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

    // HR4 — Skill Differential (design doc §2.8): perfect-square scaling, capped at ±5.
    const int SkillDifferentialCap = 5;

    // HR6 / §2.9 — Position Modifier: rear attack pulls this many weight units into SolidHit only.
    const float RearAttackWeight = 50f;

    // AV4 — Riposte counter-attack damage, relative to a normal Solid-Hit-tier result (doc: TBD %).
    const float RiposteDamageMultiplier = 0.5f;

    // 3.1.5-era class base-level interpolation range: Level 1 → Level 20 target table (§2.11).
    // Levels above 20 hold at the Level 20 table until real high-level data exists.
    const int ClassTableTopLevel = 20;

    // ── Public shapes ────────────────────────────────────────────────────────────────────────────────

    public struct Combatant
    {
        public CombatTierTable BaseTable;
        public int   Level;
        public int   Skill;
        public float Agility;
        public float Dexterity;
    }

    public struct AttackContext
    {
        public Combatant Attacker;
        public Combatant Defender;
        public bool  IsRearAttack;
        public bool  IsParryable;
        public float WeaponBaseDamage;
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
    /// authored <see cref="MobDefinition"/> combat fields, HR5/AV3) or a player (interpolates the
    /// character's class table by level, HR2, and reads weapon skill for <paramref name="category"/>).
    /// <paramref name="category"/> is the weapon category of the swing being resolved — used to look up
    /// this combatant's skill in that category, whether they're attacking or defending.
    /// </summary>
    public static Combatant BuildCombatant(GameObject go, WeaponCategory category)
    {
        var mobApp = go.GetComponent<MobApplicator>();
        if (mobApp != null && mobApp.Definition != null)
        {
            var def = mobApp.Definition;
            return new Combatant
            {
                BaseTable = def.combatTable,
                Level     = def.mobLevel,
                Skill     = def.weaponSkill,
                Agility   = def.avoidanceAgility,
                Dexterity = def.avoidanceDexterity,
            };
        }

        var exp   = go.GetComponent<PlayerExperience>();
        var pws   = go.GetComponent<PlayerWeaponSkills>();
        var stats = go.GetComponent<CharacterStats>();
        int level = exp != null ? exp.Level : 1;
        var cls   = exp != null ? exp.CurrentClass : null;

        CombatTierTable table = cls != null
            ? CombatTierTable.Lerp(cls.combatTierTableLevel1, cls.combatTierTableLevel20,
                Mathf.Clamp01((Mathf.Min(level, ClassTableTopLevel) - 1) / (float)(ClassTableTopLevel - 1)))
            : CombatTierTable.WarriorLevel1;

        return new Combatant
        {
            BaseTable = table,
            Level     = level,
            Skill     = pws != null ? pws.For(category) : 0,
            Agility   = stats != null ? stats.Agi : 0f,
            Dexterity = stats != null ? stats.Dex : 0f,
        };
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
        var table = ctx.Attacker.BaseTable;
        ApplyLevelDifferential(ref table, ctx.Attacker.Level, ctx.Defender.Level);
        ApplySkillDifferential(ref table, ctx.Attacker.Skill - ctx.Defender.Skill);
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
        int damage = tier == HitTier.Miss ? 0 : ComputeDamage(tier, ctx.WeaponBaseDamage, ctx.RelevantStat);

        // Step 4 — Mitigation (5.1.4, stub — no-op seam)
        damage = ApplyMitigation(damage, ctx.Defender);

        int riposteDamage = 0;
        if (riposted)
        {
            // AV4: the counter attack uses Step 3 only — bypasses Step 2 (already resolved) and Step 4
            // (no mitigation), at a reduced multiplier relative to a normal Solid Hit.
            riposteDamage = ComputeDamage(HitTier.SolidHit, ctx.WeaponBaseDamage, ctx.RelevantStat, RiposteDamageMultiplier);
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

    static void ApplySkillDifferential(ref CombatTierTable table, int skillDiff)
    {
        if (skillDiff == 0) return;
        int clamped  = Mathf.Clamp(skillDiff, -SkillDifferentialCap, SkillDifferentialCap);
        float weight = clamped * clamped; // perfect-square scaling, sign handled below

        if (clamped > 0) ShiftTowardPotency(ref table, weight);
        else             ShiftTowardFutility(ref table, weight);
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

    // Shared directional-weight-transfer helpers, reused by both Level and Skill differential (they both
    // push the same table "toward futility" or "toward potency" — only the magnitude differs). Crippling
    // never receives weight here — it's class-passive-unlock-only (§2.3/§2.11) and no passive system
    // exists yet; the doc itself says pre-unlock those points redistribute into Critical instead.
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
        // Riposte
        if (Random.Range(0f, 100f) < AvoidanceCurve.Evaluate(defender.Dexterity))
            return (true, true);

        // Parry — skipped for non-parryable attacks (beast bites, etc., AV3), falls through to Dodge.
        if (isParryable && Random.Range(0f, 100f) < AvoidanceCurve.Evaluate(defender.Dexterity))
            return (true, false);

        // Dodge
        if (Random.Range(0f, 100f) < AvoidanceCurve.Evaluate(defender.Agility))
            return (true, false);

        return (false, false);
    }

    // ── Step 3 — Damage (5.1.3) ──────────────────────────────────────────────────────────────────────

    static int ComputeDamage(HitTier tier, float weaponBaseDamage, float relevantStat, float extraMultiplier = 1f)
    {
        if (tier == HitTier.Miss) return 0;

        var config = CombatTierDamageConfig.Active;
        float pct      = config.PercentFor(tier);
        float variance = 1f + Random.Range(-config.variance, config.variance);

        float baseDmg = weaponBaseDamage * (1f + relevantStat / 400f);
        float raw     = baseDmg * pct * variance * extraMultiplier;
        return Mathf.Max(1, Mathf.RoundToInt(raw));
    }

    // ── Step 4 — Mitigation (5.1.4, stub) ────────────────────────────────────────────────────────────

    // MT1 — named seam, exercised every attack, currently a pass-through. Filled in once the armor/
    // mitigation design session (doc §5) produces a real formula.
    static int ApplyMitigation(int rawDamage, Combatant defender) => rawDamage;
}
