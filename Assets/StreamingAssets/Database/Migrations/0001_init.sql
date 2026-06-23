-- 0001_init — establishes the migration ledger.
-- The runner bootstraps this table before reading applied versions, so IF NOT EXISTS
-- keeps this migration idempotent and documents schema_version as the schema origin.
CREATE TABLE IF NOT EXISTS schema_version (
    version    INTEGER     PRIMARY KEY,
    name       TEXT        NOT NULL,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
