-- 8.2 — Named/rare spawn behavior (narrowed scope): a spawn table entry can override the table's
-- default respawn timer, so a rare/named roll can pop back in slower than the trash it replaces.
-- Nullable — null means "use the table's defaultTimer," unchanged behavior for every existing entry.
ALTER TABLE spawn_table_entries
    ADD COLUMN IF NOT EXISTS respawn_base_seconds REAL,
    ADD COLUMN IF NOT EXISTS respawn_variance      REAL;
