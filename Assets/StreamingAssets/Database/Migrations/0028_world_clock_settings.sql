-- 5.12 follow-up: move the day-length / lunar-cycle tunables (previously only a Unity
-- WorldClockSettings Resources asset) into the DB so they're web-authorable, same as every
-- other tunable. Single-row settings table (id fixed to 1) rather than a list — there is only
-- ever one active world clock config. Seeded by DatabaseSeeder.SeedWorldClockSettings; WorldClock
-- prefers this row over the Resources asset when present (falls back unchanged if the row is
-- ever deleted).
CREATE TABLE IF NOT EXISTS world_clock_settings (
    id                 SMALLINT PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    day_length_minutes REAL NOT NULL DEFAULT 50,
    lunar_cycle_days   REAL NOT NULL DEFAULT 8,
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);
