-- 0033_avoidance_rework — 2026-08-13 follow-up to 5.1.5. Reworks Avoidance to mirror ATK's shape:
--
--   Defense(level) = trained, capped at level×5 (mirrors Offense exactly — PlayerAvoidanceSkills.cs)
--   AvoidanceBase  = Agility×0.1 + Defense                          (feeds Dodge only)
--   EffectiveDodge   = AvoidanceBase + Dodge   (trained, capped level×5+5 — works even untrained)
--   EffectiveParry   = Parry                    (trained, capped level×5+5 — stands alone, no base)
--   EffectiveRiposte = Riposte                  (trained, capped level×5+5 — stands alone, no base)
--
-- Fixes the pre-existing bug where Parry and Riposte were mechanically identical (both read the same
-- DEX-derived value). Replaces the per-class classes.defense_base/defense_per_level formula (now
-- orphaned, same treatment as offense_base/offense_per_level before it) — Defense is a per-character
-- trained stat now, not class-authored.

-- characters: trainable Defense/Dodge/Parry/Riposte, symmetric with offense_skill (0032).
ALTER TABLE characters ADD COLUMN IF NOT EXISTS defense_skill INT NOT NULL DEFAULT 0;
ALTER TABLE characters ADD COLUMN IF NOT EXISTS dodge_skill   INT NOT NULL DEFAULT 0;
ALTER TABLE characters ADD COLUMN IF NOT EXISTS parry_skill   INT NOT NULL DEFAULT 0;
ALTER TABLE characters ADD COLUMN IF NOT EXISTS riposte_skill INT NOT NULL DEFAULT 0;

-- mobs: avoidance_agility/avoidance_dexterity (fed Dodge / Riposte+Parry identically) → three
-- independent per-check stand-ins, one per avoidance outcome. RENAME preserves existing values for the
-- two that map cleanly; avoidance_riposte is new (defaults to the same low baseline the other two use).
ALTER TABLE mobs RENAME COLUMN avoidance_agility   TO avoidance_dodge;
ALTER TABLE mobs RENAME COLUMN avoidance_dexterity TO avoidance_parry;
ALTER TABLE mobs ADD COLUMN IF NOT EXISTS avoidance_riposte REAL NOT NULL DEFAULT 20;
