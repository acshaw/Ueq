-- 0031_offense_defense_fields — follow-up to 5.1.5. Reframes the bare ATK-tuning-knob pair as a named
-- "Offense" stat (same base+per-level shape as the HP/Mana formulas already on this table), and adds a
-- symmetric "Defense" stat feeding Avoidance the same way — both per user feedback that 0030's
-- class_atk_base/atk_per_level felt like disconnected authored knobs rather than a real, class-scaled
-- stat, and that avoidance_agility/avoidance_dexterity (flat, static regardless of level) should work the
-- same way ATK now does.
--
-- RENAME preserves the values already set on Warrior/Cleric/Wizard (15/6, 8/3, 5/2) — no data loss.
ALTER TABLE classes RENAME COLUMN class_atk_base TO offense_base;
ALTER TABLE classes RENAME COLUMN atk_per_level  TO offense_per_level;

-- Defense(level) = defense_base + (level-1) * defense_per_level. Feeds Avoidance as
-- EffectiveDefense = Defense(level) + Agility×0.1 (Dodge) or + Dexterity×0.1 (Parry/Riposte) — mirrors
-- ATK's EffectiveSkill exactly (CombatResolver.BuildCombatant). Mobs are unaffected — they keep authoring
-- avoidance_agility/avoidance_dexterity directly (AV3), same "no stats to derive from" reasoning as ATK/AD3.
ALTER TABLE classes ADD COLUMN IF NOT EXISTS defense_base      REAL NOT NULL DEFAULT 5;
ALTER TABLE classes ADD COLUMN IF NOT EXISTS defense_per_level REAL NOT NULL DEFAULT 1;
