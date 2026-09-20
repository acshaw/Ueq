-- 8.1 — World Clock calendar system (Age/Year/Month/Week/Day). Extends the existing singleton
-- world_clock_settings row with persisted calendar state (8.1.1/8.1.3) and adds two small fixed-
-- cardinality lookup tables for the calendar's display names (8.1.6). Also extends
-- spawn_table_entries with the two new calendar-axis spawn conditions (8.1.4).

-- calendar_elapsed_seconds: accumulated simulated seconds since the world's Day 1 — the persisted
-- epoch anchor (8.1.1, CAL1). age/age_started_elapsed_days/age_starting_year_display back the
-- age-relative calendar display (8.1.3, CAL6a/CAL7). Defaults produce a fresh world at Age 1, Day 1,
-- "Year 1372" with no extra seeding step.
ALTER TABLE world_clock_settings
    ADD COLUMN IF NOT EXISTS calendar_elapsed_seconds  DOUBLE PRECISION NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS age                        INT NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS age_started_elapsed_days   BIGINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS age_starting_year_display  INT NOT NULL DEFAULT 1372;

-- The stated calendar math (7-day week, 4-week/28-day month) is deliberately equal to the lunar
-- cycle. The live dev DB's seeded row already carries lunar_cycle_days = 28 (DatabaseSeeder), but the
-- raw column default (migration 0028) still says 8 — bumped here for consistency in case a row is
-- ever inserted without an explicit value.
ALTER TABLE world_clock_settings ALTER COLUMN lunar_cycle_days SET DEFAULT 28;

-- Calendar display names (8.1.6, CAL8) — renameable from the web editor without a rebuild. Fixed
-- cardinality (the calendar math fixes the count at 7/13 forever), not an open-ended content list.
CREATE TABLE IF NOT EXISTS world_clock_day_names (
    day_index SMALLINT PRIMARY KEY CHECK (day_index BETWEEN 1 AND 7),
    name      TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS world_clock_month_names (
    month_index SMALLINT PRIMARY KEY CHECK (month_index BETWEEN 1 AND 13),
    name        TEXT NOT NULL
);

-- Calendar-aware spawn conditions (8.1.4). time_condition/lunar_condition are the original scope
-- (day/night, full/new moon — small fixed enums, stored as text like mobs.aggro_max_standing).
-- day_of_week_condition/month_condition are new (TG5) — numeric ranges, so nullable ints (NULL = Any)
-- instead of invented string constants.
ALTER TABLE spawn_table_entries
    ADD COLUMN IF NOT EXISTS time_condition        TEXT NOT NULL DEFAULT 'Any',
    ADD COLUMN IF NOT EXISTS lunar_condition       TEXT NOT NULL DEFAULT 'Any',
    ADD COLUMN IF NOT EXISTS day_of_week_condition INT NULL,
    ADD COLUMN IF NOT EXISTS month_condition       INT NULL;
