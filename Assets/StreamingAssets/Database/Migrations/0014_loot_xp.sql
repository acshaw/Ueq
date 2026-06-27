-- 0014_loot_xp — M2.7. Loot tables (weighted item pool + drop-count distribution + coin tiers) and the
-- single shared 50-level XP curve. Server-only content (loot is rolled server-side in Corpse; XP is
-- computed server-side). Loot items reference items.item_id (2.2); mobs reference loot_tables by id.
-- Schema only; reference rows (Giant Rat loot + the XP curve) seeded idempotently by DatabaseSeeder.

CREATE TABLE IF NOT EXISTS loot_tables (
    loot_table_id TEXT PRIMARY KEY,        -- "Giant Rat Loot Table"
    display_name  TEXT NOT NULL DEFAULT '',
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS loot_table_items (
    id            BIGSERIAL PRIMARY KEY,
    loot_table_id TEXT NOT NULL REFERENCES loot_tables(loot_table_id) ON DELETE CASCADE,
    item_id       TEXT NOT NULL,           -- references items.item_id (soft ref)
    weight        INT  NOT NULL DEFAULT 1,
    sort_order    INT  NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS loot_table_drop_counts (
    id            BIGSERIAL PRIMARY KEY,
    loot_table_id TEXT NOT NULL REFERENCES loot_tables(loot_table_id) ON DELETE CASCADE,
    count         INT  NOT NULL DEFAULT 0,
    weight        INT  NOT NULL DEFAULT 1,
    sort_order    INT  NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS loot_table_coin_tiers (
    id            BIGSERIAL PRIMARY KEY,
    loot_table_id TEXT NOT NULL REFERENCES loot_tables(loot_table_id) ON DELETE CASCADE,
    min_copper    INT  NOT NULL DEFAULT 0,
    max_copper    INT  NOT NULL DEFAULT 0,
    weight        INT  NOT NULL DEFAULT 1,
    sort_order    INT  NOT NULL DEFAULT 0
);

-- Single shared XP curve — one row per level (DL2). xp_to_next = XP to advance from `level` to `level+1`.
CREATE TABLE IF NOT EXISTS xp_levels (
    level       INT PRIMARY KEY,
    xp_to_next  INT NOT NULL
);
