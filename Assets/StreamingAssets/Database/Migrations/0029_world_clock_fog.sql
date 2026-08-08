-- 5.12 follow-up: distance fog tunables, added to the same world_clock_settings singleton row as the
-- day-length/lunar-cycle config — one "environment settings" row, not a second near-identical table.
-- Defaults match SkyDriver's own hardcoded fallback (120/520) so a pre-existing seeded row picks up
-- sane values immediately.
ALTER TABLE world_clock_settings ADD COLUMN fog_start_distance REAL NOT NULL DEFAULT 120;
ALTER TABLE world_clock_settings ADD COLUMN fog_end_distance   REAL NOT NULL DEFAULT 520;
