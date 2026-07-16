-- 0022_mob_combat_fields — 5.1.1/5.1.2/2.12(SK5). Adds the mob-side combat pipeline data: weapon
-- category/skill (Skill Differential, mirrors 2.12's player-side PlayerWeaponSkills), this mob's own
-- 7-tier hit-weight table (Hit Roll base table, HR5 — authored per mob, not derived from mob_level),
-- whether its attack can be Parried (AV3 — false for beast/unarmed-style attacks), and Agility/Dexterity
-- stand-ins for Dodge/Parry/Riposte avoidance rolls (mobs have no CharacterStats).
--
-- tier_* defaults mirror the Warrior Level 1 starting table (design doc §2.5) so a newly created mob
-- starts from a valid, non-degenerate table rather than all-zero weights.
ALTER TABLE mobs ADD COLUMN IF NOT EXISTS weapon_category INT NOT NULL DEFAULT 0; -- WeaponCategory: 0 Might, 1 Finesse
ALTER TABLE mobs ADD COLUMN IF NOT EXISTS weapon_skill    INT NOT NULL DEFAULT 0;

ALTER TABLE mobs ADD COLUMN IF NOT EXISTS tier_miss      REAL NOT NULL DEFAULT 17.5;
ALTER TABLE mobs ADD COLUMN IF NOT EXISTS tier_glancing  REAL NOT NULL DEFAULT 40;
ALTER TABLE mobs ADD COLUMN IF NOT EXISTS tier_hit       REAL NOT NULL DEFAULT 30;
ALTER TABLE mobs ADD COLUMN IF NOT EXISTS tier_solid     REAL NOT NULL DEFAULT 10;
ALTER TABLE mobs ADD COLUMN IF NOT EXISTS tier_good      REAL NOT NULL DEFAULT 2.5;
ALTER TABLE mobs ADD COLUMN IF NOT EXISTS tier_critical  REAL NOT NULL DEFAULT 0;
ALTER TABLE mobs ADD COLUMN IF NOT EXISTS tier_crippling REAL NOT NULL DEFAULT 0;

ALTER TABLE mobs ADD COLUMN IF NOT EXISTS attack_is_parryable BOOLEAN NOT NULL DEFAULT true;

ALTER TABLE mobs ADD COLUMN IF NOT EXISTS avoidance_agility   REAL NOT NULL DEFAULT 20;
ALTER TABLE mobs ADD COLUMN IF NOT EXISTS avoidance_dexterity REAL NOT NULL DEFAULT 20;
