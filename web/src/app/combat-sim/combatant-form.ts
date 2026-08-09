import { Class } from '../class.service';
import { Mob } from '../mob.service';
import { Item } from '../item.service';
import { Combatant, TierTable, computeClassHp, interpolateClassTable, weaponSkillCap } from './combat-math';

/**
 * The editable state of one side of the simulator. Deliberately a flat, freely-mutable snapshot —
 * "Load class/mob/weapon" fills it in once from live content-DB data, then every field (including
 * ones the load touched) stays a plain editable number so the user can hand-tune from there. No
 * hidden re-derivation on later edits (e.g. changing level does NOT retroactively re-interpolate the
 * tier table) — predictable beats clever for a design-iteration tool.
 */
export interface CombatantForm {
  label: string;
  level: number;
  weaponCategory: 0 | 1; // 0 Might (STR), 1 Finesse (DEX)
  weaponBaseDamage: number;
  weaponDelay: number;
  relevantStat: number;
  weaponSkill: number;
  tier: TierTable;
  avoidanceAgility: number;
  avoidanceDexterity: number;
  attackIsParryable: boolean;
  maxHp: number;
  isRearAttack: boolean;

  // Stashed source stats (set only by "Load class…") so toggling weapon category can recompute the
  // relevant stat without requiring a reload. Null until a class has been loaded.
  sourceStr: number | null;
  sourceDex: number | null;
}

export function emptyCombatantForm(label: string): CombatantForm {
  return {
    label, level: 1, weaponCategory: 0, weaponBaseDamage: 10, weaponDelay: 2,
    relevantStat: 10, weaponSkill: 0,
    tier: { miss: 17.5, glancing: 40, hit: 30, solid: 10, good: 2.5, critical: 0, crippling: 0 },
    avoidanceAgility: 10, avoidanceDexterity: 10, attackIsParryable: true, maxHp: 100,
    isRearAttack: false, sourceStr: null, sourceDex: null,
  };
}

export function toCombatant(f: CombatantForm): Combatant {
  return { baseTable: f.tier, level: f.level, skill: f.weaponSkill, agility: f.avoidanceAgility, dexterity: f.avoidanceDexterity };
}

export function loadFromClass(f: CombatantForm, cls: Class): void {
  const l1: TierTable = {
    miss: cls.tierL1Miss, glancing: cls.tierL1Glancing, hit: cls.tierL1Hit, solid: cls.tierL1Solid,
    good: cls.tierL1Good, critical: cls.tierL1Critical, crippling: cls.tierL1Crippling,
  };
  const l20: TierTable = {
    miss: cls.tierL20Miss, glancing: cls.tierL20Glancing, hit: cls.tierL20Hit, solid: cls.tierL20Solid,
    good: cls.tierL20Good, critical: cls.tierL20Critical, crippling: cls.tierL20Crippling,
  };
  const level = f.level || 1;
  f.tier = interpolateClassTable(l1, l20, level);
  f.avoidanceAgility = cls.baseAgi;
  f.avoidanceDexterity = cls.baseDex;
  f.sourceStr = cls.baseStr;
  f.sourceDex = cls.baseDex;
  f.relevantStat = f.weaponCategory === 0 ? cls.baseStr : cls.baseDex;
  f.weaponSkill = weaponSkillCap(level);
  f.maxHp = computeClassHp(cls, level);
  f.label = `${cls.className} (L${level})`;
}

export function loadFromMob(f: CombatantForm, mob: Mob): void {
  f.level = mob.mobLevel;
  f.weaponCategory = mob.weaponCategory === 1 ? 1 : 0;
  f.weaponBaseDamage = mob.attackDamage;
  f.weaponDelay = mob.attackInterval;
  f.relevantStat = 0; // mobs: attackDamage already represents full power, no STR/DEX (matches EnemyAI)
  f.weaponSkill = mob.weaponSkill;
  f.tier = {
    miss: mob.tierMiss, glancing: mob.tierGlancing, hit: mob.tierHit, solid: mob.tierSolid,
    good: mob.tierGood, critical: mob.tierCritical, crippling: mob.tierCrippling,
  };
  f.avoidanceAgility = mob.avoidanceAgility;
  f.avoidanceDexterity = mob.avoidanceDexterity;
  f.attackIsParryable = mob.attackIsParryable;
  f.maxHp = mob.maxHealth;
  f.sourceStr = null;
  f.sourceDex = null;
  f.label = `${mob.displayName} (L${mob.mobLevel})`;
}

export function loadFromWeapon(f: CombatantForm, item: Item): void {
  f.weaponBaseDamage = item.weaponBaseDamage;
  f.weaponDelay = item.weaponDelay;
  f.weaponCategory = item.weaponCategory === 1 ? 1 : 0;
  recomputeRelevantStat(f);
}

export function recomputeRelevantStat(f: CombatantForm): void {
  if (f.sourceStr != null && f.sourceDex != null) {
    f.relevantStat = f.weaponCategory === 0 ? f.sourceStr : f.sourceDex;
  }
}
