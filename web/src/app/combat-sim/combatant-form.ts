import { Class } from '../class.service';
import { Mob } from '../mob.service';
import { Item } from '../item.service';
import { Combatant, TierTable, computeAtk, computeClassHp, defenseCap, effectiveDodge, offenseCap, resolveAtkTable, weaponSkillCap } from './combat-math';

/**
 * The editable state of one side of the simulator. Deliberately a flat, freely-mutable snapshot —
 * "Load class/mob/weapon" fills it in once from live content-DB data, then every field (including
 * ones the load touched) stays a plain editable number so the user can hand-tune from there. No
 * hidden re-derivation on later edits (e.g. changing level does NOT retroactively re-interpolate the
 * tier table) — predictable beats clever for a design-iteration tool.
 *
 * 5.1.5, revised repeatedly (2026-08-13): the hit-tier table is derived live from ATK. `weaponSkill`
 * and `offense` are real editable fields on this form (mirroring the game's trainable
 * `PlayerWeaponSkills`/`PlayerOffense` stats), clamped to that level's cap (`weaponSkillCap`/
 * `offenseCap`) whenever edited — see `clampWeaponSkill`/`clampOffense` below, used by the panel's
 * input handlers. "Load class" defaults both to their level's cap (assume fully trained), same as the
 * game's own real characters can reach through use — but unlike a fixed formula, either can be hand-
 * lowered here to see how an undertrained character performs. A mob instead sets `manualAtk` — its ATK
 * is authored directly (AD3), bypassing weaponSkill/offense entirely.
 *
 * Avoidance rework (2026-08-13): `defense`/`dodgeSkill`/`parrySkill`/`riposteSkill` are the same
 * treatment — real editable fields, clamped to their level's cap, defaulting to fully-trained on "Load
 * class". A mob instead sets `manualDodge`/`manualParry`/`manualRiposte` — its avoidance is authored
 * directly per check (AV3), bypassing the whole AvoidanceBase/skill formula, mirroring `manualAtk`.
 */
export interface CombatantForm {
  label: string;
  level: number;
  weaponCategory: 0 | 1; // 0 Might (STR), 1 Finesse (DEX)
  weaponSkill: number;   // trained, capped at weaponSkillCap(level) — see clampWeaponSkill
  offense: number;       // trained, capped at offenseCap(level) — see clampOffense
  weaponBaseDamage: number;
  weaponBonusDamage: number; // stat-scalable portion (2026-08-21) — see relevantStatForAtk/computeDamage
  weaponDelay: number;
  defense: number;       // trained, capped at defenseCap(level) — feeds Dodge's AvoidanceBase only
  dodgeSkill: number;    // trained, capped at weaponSkillCap(level) — added on top of AvoidanceBase
  parrySkill: number;    // trained, capped at weaponSkillCap(level) — stands alone, no base
  riposteSkill: number;  // trained, capped at weaponSkillCap(level) — stands alone, no base
  attackIsParryable: boolean;
  maxHp: number;
  isRearAttack: boolean;

  // 2026-08-21 (Mitigation) — sole mitigation lever. No formula for either players or mobs — a
  // player's real AC is just the sum of equipped gear (not modeled by this simulator, same as weapon
  // fields), a mob's is authored directly. Freely editable, defaults to 0 on "Load class".
  ac: number;

  // Raw character stats — same seven fields the Class Editor authors (baseStr/baseSta/.../baseCha).
  str: number;
  sta: number;
  agi: number;
  dex: number;
  int: number;
  wis: number;
  cha: number;

  // AD3 — set by "Load mob" to the mob's own authored ATK; null for a class-based combatant, where ATK
  // is computed from weaponSkill + offense + the relevant raw stat.
  manualAtk: number | null;

  // AV3 — set by "Load mob" to the mob's own authored per-check avoidance numbers; null for a
  // class-based combatant, where each is computed from AvoidanceBase + the trained skill (dodge) or the
  // trained skill alone (parry/riposte).
  manualDodge: number | null;
  manualParry: number | null;
  manualRiposte: number | null;
}

export function emptyCombatantForm(label: string): CombatantForm {
  return {
    label, level: 1, weaponCategory: 0, weaponSkill: weaponSkillCap(1), offense: offenseCap(1),
    weaponBaseDamage: 10, weaponBonusDamage: 0, weaponDelay: 2,
    defense: defenseCap(1), dodgeSkill: weaponSkillCap(1), parrySkill: weaponSkillCap(1), riposteSkill: weaponSkillCap(1),
    attackIsParryable: true, maxHp: 100, isRearAttack: false, ac: 0,
    str: 10, sta: 10, agi: 10, dex: 10, int: 10, wis: 10, cha: 10,
    manualAtk: null, manualDodge: null, manualParry: null, manualRiposte: null,
  };
}

/** STR for a Might weapon, DEX for a Finesse weapon — feeds both ATK's EffectiveSkill term and Damage. */
export function relevantStatForAtk(f: CombatantForm): number {
  return f.weaponCategory === 0 ? f.str : f.dex;
}

/** Clamp a hand-edited weaponSkill to [0, weaponSkillCap(level)] — call from the input's change handler. */
export function clampWeaponSkill(value: number, level: number): number {
  return Math.max(0, Math.min(value, weaponSkillCap(level)));
}

/** Clamp a hand-edited offense to [0, offenseCap(level)] — call from the input's change handler. */
export function clampOffense(value: number, level: number): number {
  return Math.max(0, Math.min(value, offenseCap(level)));
}

/** Clamp a hand-edited Defense to [0, defenseCap(level)] — call from the input's change handler. */
export function clampDefense(value: number, level: number): number {
  return Math.max(0, Math.min(value, defenseCap(level)));
}

/** Clamp a hand-edited Dodge/Parry/Riposte skill to [0, weaponSkillCap(level)] — same cap shape as
 * WeaponSkill (level×5+5). Call from the input's change handler. */
export function clampAvoidanceSkill(value: number, level: number): number {
  return Math.max(0, Math.min(value, weaponSkillCap(level)));
}

export function atk(f: CombatantForm): number {
  if (f.manualAtk != null) return f.manualAtk;
  return computeAtk(f.weaponSkill, relevantStatForAtk(f), f.offense);
}

export function resolvedTable(f: CombatantForm): TierTable {
  return resolveAtkTable(atk(f));
}

export function dodgeValue(f: CombatantForm): number {
  return f.manualDodge != null ? f.manualDodge : effectiveDodge(f.agi, f.defense, f.dodgeSkill);
}

export function parryValue(f: CombatantForm): number {
  return f.manualParry != null ? f.manualParry : f.parrySkill;
}

export function riposteValue(f: CombatantForm): number {
  return f.manualRiposte != null ? f.manualRiposte : f.riposteSkill;
}

export function toCombatant(f: CombatantForm): Combatant {
  return {
    atk: atk(f), level: f.level,
    dodge: dodgeValue(f), parry: parryValue(f), riposte: riposteValue(f),
    ac: f.ac,
  };
}

export function loadFromClass(f: CombatantForm, cls: Class): void {
  const level = f.level || 1;
  f.manualAtk = null;
  f.manualDodge = null;
  f.manualParry = null;
  f.manualRiposte = null;
  f.weaponSkill = weaponSkillCap(level); // assume fully trained — see class doc comment above
  f.offense = offenseCap(level);
  f.defense      = defenseCap(level);
  f.dodgeSkill   = weaponSkillCap(level);
  f.parrySkill   = weaponSkillCap(level);
  f.riposteSkill = weaponSkillCap(level);
  f.str = cls.baseStr;
  f.sta = cls.baseSta;
  f.agi = cls.baseAgi;
  f.dex = cls.baseDex;
  f.int = cls.baseInt;
  f.wis = cls.baseWis;
  f.cha = cls.baseCha;
  f.maxHp = computeClassHp(cls, level);
  f.ac = 0; // no class/race base — a real character's AC comes entirely from equipped gear
  f.label = `${cls.className} (L${level})`;
}

export function loadFromMob(f: CombatantForm, mob: Mob): void {
  f.level = mob.mobLevel;
  f.weaponCategory = mob.weaponCategory === 1 ? 1 : 0;
  f.weaponBaseDamage = mob.attackDamage;
  f.weaponBonusDamage = 0; // mobs: RelevantStat is always 0 (no CharacterStats), so bonus damage is moot
  f.weaponDelay = mob.attackInterval;
  // AD3 — mobs author ATK directly as one number; no formula involved, weaponSkill/offense unused.
  f.manualAtk = mob.atk;
  f.weaponSkill = 0;
  f.offense = 0;
  f.str = 0; // mobs: attackDamage already represents full power, no STR/DEX (matches EnemyAI)
  f.sta = 0;
  f.agi = 0;
  f.int = 0;
  f.wis = 0;
  f.cha = 0;
  // AV3 — same reasoning: mobs author each avoidance check directly as a flat number, bypassing
  // AvoidanceBase/the trained skills entirely (see combat-math.ts / dodgeValue/parryValue/riposteValue).
  f.manualDodge   = mob.avoidanceDodge;
  f.manualParry   = mob.avoidanceParry;
  f.manualRiposte = mob.avoidanceRiposte;
  f.defense = 0;
  f.dodgeSkill = 0;
  f.parrySkill = 0;
  f.riposteSkill = 0;
  f.attackIsParryable = mob.attackIsParryable;
  f.maxHp = mob.maxHealth;
  f.ac = mob.ac; // 2026-08-21 — authored directly, same treatment as manualAtk/manualDodge etc.
  f.label = `${mob.displayName} (L${mob.mobLevel})`;
}

export function loadFromWeapon(f: CombatantForm, item: Item): void {
  f.weaponBaseDamage = item.weaponBaseDamage;
  f.weaponBonusDamage = item.weaponBonusDamage;
  f.weaponDelay = item.weaponDelay;
  f.weaponCategory = item.weaponCategory === 1 ? 1 : 0;
}
