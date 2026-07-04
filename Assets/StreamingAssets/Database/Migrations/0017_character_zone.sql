-- 0017_character_zone — M3.0. Zone integration: a character remembers which zone it last stood in, so
-- login restores the player into the right zone (and pos/yaw are reinterpreted in that zone's world-space
-- offset). Additive + non-breaking: existing characters default into the starter zone (Creslin's Field).
ALTER TABLE characters ADD COLUMN IF NOT EXISTS zone_id TEXT NOT NULL DEFAULT 'creslins_field';
