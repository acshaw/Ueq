-- 0034_weapon_bonus_damage — Damage step rework (2026-08-21): Damage = (RelevantStat x 0.01 x
-- WeaponBonusDamage) + WeaponBaseDamage, replacing the old WeaponBaseDamage x (1 + RelevantStat/400)
-- term. WeaponBonusDamage is the stat-scalable portion of a weapon's damage; WeaponBaseDamage stays
-- flat regardless of STR/DEX. No cap on the stat->percentage conversion — itemization determines the
-- practical ceiling.
ALTER TABLE items ADD COLUMN IF NOT EXISTS weapon_bonus_damage INT NOT NULL DEFAULT 0;
