/**
 * TypeScript port of the Unity-side combat pipeline (Assets/Scripts/Combat/CombatResolver.cs,
 * CombatTierTable.cs, AvoidanceCurve.cs, CombatTierDamageConfig.cs — the "Eventide" pipeline,
 * docs/eventide_combat_pipeline.md). Kept in lockstep with that C# by hand — there is no shared
 * source of truth between Unity and this web app, so any change to the tuning constants or the
 * pipeline steps below must be mirrored back into CombatResolver.cs (and vice versa).
 *
 * Runs entirely client-side (Monte Carlo over plain JS loops) so combat-design iteration in the
 * simulator tool is instant — no server round trip, no schema, no persistence.
 */

// ── Hit tiers (design doc §2.2/§2.3) ──────────────────────────────────────────────────────────────

export const TIER_ORDER = ['miss', 'glancing', 'hit', 'solid', 'good', 'critical', 'crippling'] as const;
export type HitTier = (typeof TIER_ORDER)[number];

export const TIER_LABELS: Record<HitTier, string> = {
  miss: 'Miss', glancing: 'Glancing', hit: 'Hit', solid: 'Solid Hit',
  good: 'Good Hit', critical: 'Critical', crippling: 'Crippling',
};

/** Sequential single-hue ramp (Material Deep Purple tonal steps, light→dark), one entry per tier in
 * potency order — a magnitude/ordinal encoding, not a categorical one (dataviz skill: "sequential =
 * one hue, light→dark"). Matches the app's existing indigo/purple accent (#673ab7 = ~critical). */
export const TIER_COLORS: Record<HitTier, string> = {
  miss: '#ede7f6', glancing: '#d1c4e9', hit: '#b39ddb', solid: '#9575cd',
  good: '#7e57c2', critical: '#673ab7', crippling: '#4527a0',
};

export type TierTable = Record<HitTier, number>;

export function tierTotal(t: TierTable): number {
  return TIER_ORDER.reduce((sum, k) => sum + t[k], 0);
}

export function cloneTable(t: TierTable): TierTable {
  return { ...t };
}

export function addTier(t: TierTable, tier: HitTier, delta: number): void {
  t[tier] = Math.max(0, t[tier] + delta);
}

export function lerpTable(a: TierTable, b: TierTable, f: number): TierTable {
  const out = {} as TierTable;
  for (const k of TIER_ORDER) out[k] = a[k] + (b[k] - a[k]) * f;
  return out;
}

/** Warrior Level 1 starting table (design doc §2.5) — last-resort fallback, mirrors CombatTierTable.WarriorLevel1. */
export const WARRIOR_LEVEL_1: TierTable = {
  miss: 17.5, glancing: 40, hit: 30, solid: 10, good: 2.5, critical: 0, crippling: 0,
};

/** 3.1.5-era class interpolation range: Level 1 → Level 20 target table; holds at L20 above that. */
const CLASS_TABLE_TOP_LEVEL = 20;

export function interpolateClassTable(l1: TierTable, l20: TierTable, level: number): TierTable {
  const f = Math.max(0, Math.min(1, (Math.min(level, CLASS_TABLE_TOP_LEVEL) - 1) / (CLASS_TABLE_TOP_LEVEL - 1)));
  return lerpTable(l1, l20, f);
}

// ── Avoidance curve (AV1/AV2, design doc §3.3) ────────────────────────────────────────────────────

const AVOIDANCE_POINTS: [stat: number, pct: number][] = [
  [1, 0.10], [50, 0.10], [51, 0.136], [75, 1.0], [76, 1.16],
  [100, 5.0], [101, 5.14], [135, 10.0], [136, 10.07], [209, 14.93], [210, 15.0],
];

export function avoidanceChance(stat: number): number {
  const pts = AVOIDANCE_POINTS;
  if (stat <= pts[0][0]) return pts[0][1];
  if (stat >= pts[pts.length - 1][0]) return pts[pts.length - 1][1];
  for (let i = 0; i < pts.length - 1; i++) {
    const [s0, p0] = pts[i];
    const [s1, p1] = pts[i + 1];
    if (stat >= s0 && stat <= s1) {
      const t = (stat - s0) / (s1 - s0);
      return p0 + (p1 - p0) * t;
    }
  }
  return pts[pts.length - 1][1];
}

// ── Damage config (DM1, design doc §4.1) ──────────────────────────────────────────────────────────

export const TIER_DAMAGE_PERCENT: Record<HitTier, number> = {
  miss: 0, glancing: 0.25, hit: 0.60, solid: 1.00, good: 1.10, critical: 1.25, crippling: 1.50,
};
export const DAMAGE_VARIANCE = 0.125; // ± band per swing

// ── Tuning constants (named per CombatResolver.cs) ────────────────────────────────────────────────

const LEVEL_BAND_FLOOR = [1, 2, 3, 5, 8, 13];
const LEVEL_BAND_INCREMENTS = [1, 1, 2, 3, 5, 8];
const MAX_LEVEL_FUTILITY_WEIGHT = 100;
const LEVEL_ADVANTAGE_SCALE = 0.5;
const SKILL_DIFFERENTIAL_CAP = 5;
const REAR_ATTACK_WEIGHT = 50;
const RIPOSTE_DAMAGE_MULTIPLIER = 0.5;

const FUTILITY_DRAIN: HitTier[] = ['crippling', 'critical', 'good', 'solid', 'hit'];
const FUTILITY_FILL: HitTier[] = ['miss', 'glancing'];
const POTENCY_DRAIN: HitTier[] = ['miss', 'glancing', 'hit'];
const POTENCY_FILL: HitTier[] = ['solid', 'good', 'critical'];

function transfer(table: TierTable, amount: number, drainFrom: HitTier[], fillInto: HitTier[]): void {
  if (amount <= 0) return;
  let available = 0;
  for (const t of drainFrom) available += table[t];
  const moved = Math.min(amount, available);
  if (moved <= 0) return;

  for (const t of drainFrom) {
    const have = table[t];
    if (have <= 0) continue;
    addTier(table, t, -(moved * (have / available)));
  }
  const each = moved / fillInto.length;
  for (const t of fillInto) addTier(table, t, each);
}

const shiftTowardFutility = (table: TierTable, amount: number) => transfer(table, amount, FUTILITY_DRAIN, FUTILITY_FILL);
const shiftTowardPotency = (table: TierTable, amount: number) => transfer(table, amount, POTENCY_DRAIN, POTENCY_FILL);

function incrementsToFutility(lowLevel: number): number {
  for (let i = LEVEL_BAND_FLOOR.length - 1; i >= 0; i--) {
    if (lowLevel >= LEVEL_BAND_FLOOR[i]) return LEVEL_BAND_INCREMENTS[i];
  }
  return LEVEL_BAND_INCREMENTS[0];
}

function applyLevelDifferential(table: TierTable, attackerLevel: number, defenderLevel: number): void {
  const gap = attackerLevel - defenderLevel;
  if (gap === 0) return;
  const lowLevel = Math.min(attackerLevel, defenderLevel);
  const increments = incrementsToFutility(lowLevel);
  const fraction = Math.max(0, Math.min(1, Math.abs(gap) / increments));
  if (gap < 0) shiftTowardFutility(table, fraction * MAX_LEVEL_FUTILITY_WEIGHT);
  else shiftTowardPotency(table, fraction * MAX_LEVEL_FUTILITY_WEIGHT * LEVEL_ADVANTAGE_SCALE);
}

function applySkillDifferential(table: TierTable, skillDiff: number): void {
  if (skillDiff === 0) return;
  const clamped = Math.max(-SKILL_DIFFERENTIAL_CAP, Math.min(SKILL_DIFFERENTIAL_CAP, skillDiff));
  const weight = clamped * clamped;
  if (clamped > 0) shiftTowardPotency(table, weight);
  else shiftTowardFutility(table, weight);
}

function applyPositionModifier(table: TierTable): void {
  const pool = table.miss + table.glancing + table.hit;
  if (pool <= 0) return;
  const amount = Math.min(REAR_ATTACK_WEIGHT, pool);
  const takeMiss = amount * (table.miss / pool);
  const takeGlancing = amount * (table.glancing / pool);
  const takeHit = amount * (table.hit / pool);
  table.miss -= takeMiss;
  table.glancing -= takeGlancing;
  table.hit -= takeHit;
  table.solid += takeMiss + takeGlancing + takeHit;
}

function weightedPick(table: TierTable, rng: () => number): HitTier {
  const total = tierTotal(table);
  if (total <= 0) return 'miss';
  const roll = rng() * total;
  let cumulative = 0;
  for (const t of TIER_ORDER) {
    cumulative += table[t];
    if (roll < cumulative) return t;
  }
  return 'crippling'; // float rounding fallback, mirrors the C#
}

// ── Combatant / attack shapes ──────────────────────────────────────────────────────────────────────

export interface Combatant {
  baseTable: TierTable;
  level: number;
  skill: number;
  agility: number;
  dexterity: number;
}

export interface AttackContext {
  attacker: Combatant;
  defender: Combatant;
  isRearAttack: boolean;
  isParryable: boolean;
  weaponBaseDamage: number;
  relevantStat: number;
}

export type AvoidCause = 'none' | 'dodge' | 'parry' | 'riposte';

export interface AttackResult {
  rawTier: HitTier;   // tier from the weighted table, before avoidance
  tier: HitTier;      // final tier (Miss if avoided)
  avoidCause: AvoidCause;
  damage: number;
  riposted: boolean;
  riposteDamage: number;
}

function resolveAvoidance(defender: Combatant, isParryable: boolean, rng: () => number): { avoided: boolean; riposted: boolean; cause: AvoidCause } {
  if (rng() * 100 < avoidanceChance(defender.dexterity)) return { avoided: true, riposted: true, cause: 'riposte' };
  if (isParryable && rng() * 100 < avoidanceChance(defender.dexterity)) return { avoided: true, riposted: false, cause: 'parry' };
  if (rng() * 100 < avoidanceChance(defender.agility)) return { avoided: true, riposted: false, cause: 'dodge' };
  return { avoided: false, riposted: false, cause: 'none' };
}

function computeDamage(tier: HitTier, weaponBaseDamage: number, relevantStat: number, extraMultiplier: number, rng: () => number): number {
  if (tier === 'miss') return 0;
  const pct = TIER_DAMAGE_PERCENT[tier];
  const variance = 1 + (rng() * 2 - 1) * DAMAGE_VARIANCE; // Random.Range(-v, v)
  const baseDmg = weaponBaseDamage * (1 + relevantStat / 400);
  const raw = baseDmg * pct * variance * extraMultiplier;
  return Math.max(1, Math.round(raw));
}

/** Step 4 — Mitigation (5.1.4, stub in the C# too). Named seam kept for parity; no-op today. */
function applyMitigation(damage: number, _defender: Combatant): number {
  return damage;
}

/** Mirrors CombatResolver.ResolveAttack exactly. `rng` defaults to Math.random; pass a seeded PRNG
 * for reproducible runs. */
export function resolveAttack(ctx: AttackContext, rng: () => number = Math.random): AttackResult {
  const table = cloneTable(ctx.attacker.baseTable);
  applyLevelDifferential(table, ctx.attacker.level, ctx.defender.level);
  applySkillDifferential(table, ctx.attacker.skill - ctx.defender.skill);
  if (ctx.isRearAttack) applyPositionModifier(table);

  const rawTier = weightedPick(table, rng);
  let tier = rawTier;
  let riposted = false;
  let avoidCause: AvoidCause = 'none';

  if (tier !== 'miss') {
    const avoid = resolveAvoidance(ctx.defender, ctx.isParryable, rng);
    if (avoid.avoided) {
      tier = 'miss';
      riposted = avoid.riposted;
      avoidCause = avoid.cause;
    }
  }

  let damage = tier === 'miss' ? 0 : computeDamage(tier, ctx.weaponBaseDamage, ctx.relevantStat, 1, rng);
  damage = applyMitigation(damage, ctx.defender);

  let riposteDamage = 0;
  if (riposted) {
    riposteDamage = computeDamage('solid', ctx.weaponBaseDamage, ctx.relevantStat, RIPOSTE_DAMAGE_MULTIPLIER, rng);
  }

  return { rawTier, tier, avoidCause, damage, riposted, riposteDamage };
}

// ── Monte Carlo: single-direction swing statistics ────────────────────────────────────────────────

export interface SwingStats {
  trials: number;
  tierCounts: Record<HitTier, number>;
  tierPct: Record<HitTier, number>;
  rawMissRate: number;      // clean whiffs off the table roll itself (never reached avoidance)
  dodgeRate: number;
  parryRate: number;
  riposteRate: number;
  hitRate: number;          // landed (final tier !== miss)
  avgDamagePerSwing: number;
  avgDamagePerLandedHit: number;
  dps: number;               // avgDamagePerSwing / weaponDelay
  avgRiposteDamage: number;  // average counter-damage dealt back per riposte
}

export function simulateSwings(ctx: AttackContext, weaponDelay: number, trials: number, rng: () => number = Math.random): SwingStats {
  const tierCounts = Object.fromEntries(TIER_ORDER.map(t => [t, 0])) as Record<HitTier, number>;
  let rawMiss = 0, dodge = 0, parry = 0, riposte = 0;
  let totalDamage = 0, landedHits = 0, landedDamage = 0, riposteDamageSum = 0;

  for (let i = 0; i < trials; i++) {
    const r = resolveAttack(ctx, rng);
    tierCounts[r.tier]++;
    if (r.avoidCause === 'none' && r.tier === 'miss') rawMiss++;
    else if (r.avoidCause === 'dodge') dodge++;
    else if (r.avoidCause === 'parry') parry++;
    else if (r.avoidCause === 'riposte') riposte++;

    totalDamage += r.damage;
    if (r.tier !== 'miss') { landedHits++; landedDamage += r.damage; }
    if (r.riposted) riposteDamageSum += r.riposteDamage;
  }

  const tierPct = Object.fromEntries(TIER_ORDER.map(t => [t, (tierCounts[t] / trials) * 100])) as Record<HitTier, number>;
  const avgDamagePerSwing = totalDamage / trials;

  return {
    trials,
    tierCounts,
    tierPct,
    rawMissRate: (rawMiss / trials) * 100,
    dodgeRate: (dodge / trials) * 100,
    parryRate: (parry / trials) * 100,
    riposteRate: (riposte / trials) * 100,
    hitRate: (landedHits / trials) * 100,
    avgDamagePerSwing,
    avgDamagePerLandedHit: landedHits > 0 ? landedDamage / landedHits : 0,
    dps: weaponDelay > 0 ? avgDamagePerSwing / weaponDelay : 0,
    avgRiposteDamage: riposte > 0 ? riposteDamageSum / riposte : 0,
  };
}

// ── Monte Carlo: fight-to-the-death (time-to-kill) ────────────────────────────────────────────────

export interface FightSide {
  combatant: Combatant;
  weaponBaseDamage: number;
  weaponDelay: number;
  relevantStat: number;
  isParryable: boolean; // whether THIS side's attack can be parried by the opponent
  maxHp: number;
}

export interface FightStats {
  trials: number;
  aWinRate: number;
  bWinRate: number;
  timeoutRate: number; // neither side died before the time cap (stalemate — tune inputs)
  avgTtk: number;       // seconds, decisive fights only
  medianTtk: number;
  minTtk: number;
  maxTtk: number;
  avgSurvivorHpPct: number; // winner's remaining HP%, decisive fights only
}

export function simulateFight(a: FightSide, b: FightSide, trials: number, maxSeconds = 180, rng: () => number = Math.random): FightStats {
  let aWins = 0, bWins = 0, timeouts = 0;
  const ttks: number[] = [];
  let survivorHpPctSum = 0;

  for (let i = 0; i < trials; i++) {
    let hpA = a.maxHp, hpB = b.maxHp;
    let nextA = 0, nextB = 0;
    let t = 0;

    while (hpA > 0 && hpB > 0 && t < maxSeconds) {
      if (nextA <= nextB) {
        const r = resolveAttack({
          attacker: a.combatant, defender: b.combatant,
          isRearAttack: false, isParryable: a.isParryable,
          weaponBaseDamage: a.weaponBaseDamage, relevantStat: a.relevantStat,
        }, rng);
        hpB -= r.damage;
        if (r.riposted) hpA -= r.riposteDamage;
        t = nextA;
        nextA += a.weaponDelay;
      } else {
        const r = resolveAttack({
          attacker: b.combatant, defender: a.combatant,
          isRearAttack: false, isParryable: b.isParryable,
          weaponBaseDamage: b.weaponBaseDamage, relevantStat: b.relevantStat,
        }, rng);
        hpA -= r.damage;
        if (r.riposted) hpB -= r.riposteDamage;
        t = nextB;
        nextB += b.weaponDelay;
      }
    }

    if (hpA <= 0 && hpB <= 0) { timeouts++; } // simultaneous double-KO, rare — bucket with timeout
    else if (hpB <= 0) { aWins++; ttks.push(t); survivorHpPctSum += Math.max(0, hpA / a.maxHp) * 100; }
    else if (hpA <= 0) { bWins++; ttks.push(t); survivorHpPctSum += Math.max(0, hpB / b.maxHp) * 100; }
    else { timeouts++; }
  }

  ttks.sort((x, y) => x - y);
  const decisive = ttks.length;

  return {
    trials,
    aWinRate: (aWins / trials) * 100,
    bWinRate: (bWins / trials) * 100,
    timeoutRate: (timeouts / trials) * 100,
    avgTtk: decisive > 0 ? ttks.reduce((s, v) => s + v, 0) / decisive : 0,
    medianTtk: decisive > 0 ? ttks[Math.floor(decisive / 2)] : 0,
    minTtk: decisive > 0 ? ttks[0] : 0,
    maxTtk: decisive > 0 ? ttks[decisive - 1] : 0,
    avgSurvivorHpPct: decisive > 0 ? survivorHpPctSum / decisive : 0,
  };
}

// ── Class HP formula port (Phase 2 §HP calculation — CharacterStats.cs / Health.EffectiveMax) ────────

export interface HpFormulaInputs {
  classBaseHP: number; hpPerLevel: number; staCap: number; baseStaRatio: number; staGrowthRate: number;
  baseSta: number;
}

export function computeClassHp(inp: HpFormulaInputs, level: number): number {
  const effectiveSta = Math.min(inp.baseSta, inp.staCap);
  const staModifier = inp.baseStaRatio + (level - 1) * inp.staGrowthRate;
  return Math.round(inp.classBaseHP + (level - 1) * inp.hpPerLevel + effectiveSta * staModifier);
}

/** SK3 — skillCap(level) = base + perLevel × (level − 1), mirrors PlayerWeaponSkills.Cap. */
export function weaponSkillCap(level: number, capBase = 5, capPerLevel = 5): number {
  return capBase + capPerLevel * Math.max(0, level - 1);
}
