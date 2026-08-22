-- 0030_atk_combat_fields — 5.1.5 (AD8). Adds the ATK-derived Hit Roll fields alongside the old
-- per-class/per-mob authored tier tables (not dropped yet — a follow-up migration removes the 21
-- tier_l1_*/tier_l20_*/tier_* columns once the new formula is verified in-editor and in the combat
-- simulator, matching this project's "don't remove old data until the replacement is confirmed
-- working" convention from the 2026-06-27 deprecated-field cleanup pass).
--
-- classes.class_atk_base/atk_per_level replace the 14-field Combat Tier Table (AD2): ATK =
-- EffectiveSkill (trained weapon skill + relevant stat × 0.1, §2.10) + class_atk_base +
-- (level−1) × atk_per_level.
--
-- mobs.atk replaces the 7-field tier table (AD3) — mobs have no stats to derive ATK from, so it's
-- authored directly as one number, same "author the final number" pattern as attack_damage.
ALTER TABLE classes ADD COLUMN IF NOT EXISTS class_atk_base REAL NOT NULL DEFAULT 10;
ALTER TABLE classes ADD COLUMN IF NOT EXISTS atk_per_level  REAL NOT NULL DEFAULT 4;

ALTER TABLE mobs ADD COLUMN IF NOT EXISTS atk REAL NOT NULL DEFAULT 10;
