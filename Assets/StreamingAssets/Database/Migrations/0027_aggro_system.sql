-- 0027_aggro_system — 5.4. Two additive columns:
--   1) faction_thresholds.consider_text — the player-facing message for the new Consider mechanic (AG1),
--      authored per-threshold in the web Faction Editor alongside name/min_score so a renamed/added tier
--      never silently breaks the message. Seeded defaults for the 9 existing thresholds by DatabaseSeeder.
--   2) mobs.social_aggro_enabled / social_aggro_radius — social aggro (AG3), opt-in per mob (default false,
--      zero behavior change to existing mobs) with a sensible default radius so enabling it doesn't also
--      require picking a number.
ALTER TABLE faction_thresholds ADD COLUMN IF NOT EXISTS consider_text TEXT NOT NULL DEFAULT '';

ALTER TABLE mobs ADD COLUMN IF NOT EXISTS social_aggro_enabled BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE mobs ADD COLUMN IF NOT EXISTS social_aggro_radius  REAL    NOT NULL DEFAULT 20;
