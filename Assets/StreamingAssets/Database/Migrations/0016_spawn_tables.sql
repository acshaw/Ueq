-- 0016_spawn_tables — M2.7.2. Weighted spawn tables with an inlined respawn timer (DS1). A SpawnPoint
-- references a spawn_table_id; on activation it weighted-picks an entry and spawns group_size DB mobs.
-- Entries reference mobs.mob_id (soft ref). Server-only. Schema only; the 3 existing SO spawn tables
-- are seeded idempotently by DatabaseSeeder.
CREATE TABLE IF NOT EXISTS spawn_tables (
    spawn_table_id     TEXT PRIMARY KEY,          -- "Mob Spawn Table"
    display_name       TEXT NOT NULL DEFAULT '',
    timer_base_seconds REAL NOT NULL DEFAULT 300,  -- respawn delay center
    timer_variance     REAL NOT NULL DEFAULT 0,    -- ± randomization
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS spawn_table_entries (
    id             BIGSERIAL PRIMARY KEY,
    spawn_table_id TEXT NOT NULL REFERENCES spawn_tables(spawn_table_id) ON DELETE CASCADE,
    mob_id         TEXT NOT NULL,                  -- references mobs.mob_id (soft ref)
    weight         INT  NOT NULL DEFAULT 1,
    group_size     INT  NOT NULL DEFAULT 1,        -- mobs spawned per activation
    sort_order     INT  NOT NULL DEFAULT 0
);
