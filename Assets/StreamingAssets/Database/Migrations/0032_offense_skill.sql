-- 0032_offense_skill — trainable Offense value, symmetric with might_skill/finesse_skill (0021). Unlike
-- WeaponSkill (split Might/Finesse), Offense is a single general combat-aptitude stat — starts at 1,
-- capped at level×5 (no +5, unlike WeaponSkill's level×5+5 — PlayerOffense.cs), earned through use
-- (PlayerOffense.RollOffenseUp, mirrors SK4). Replaces the fixed `Offense(level) = level×5` formula
-- CombatResolver used previously (2026-08-11) — ATK now reads this persisted, trained value directly
-- instead of assuming it's always at the cap.
ALTER TABLE characters ADD COLUMN IF NOT EXISTS offense_skill INT NOT NULL DEFAULT 0;
