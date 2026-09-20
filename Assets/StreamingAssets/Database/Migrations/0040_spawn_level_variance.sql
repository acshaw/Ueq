-- 8.3 — Level variance within one mob type at a spawn: an optional per-entry level range. Both null =
-- no variance, spawns at the mob's authored MobDefinition.mobLevel exactly (unchanged existing behavior).
ALTER TABLE spawn_table_entries
    ADD COLUMN IF NOT EXISTS min_level_override INT,
    ADD COLUMN IF NOT EXISTS max_level_override INT;
