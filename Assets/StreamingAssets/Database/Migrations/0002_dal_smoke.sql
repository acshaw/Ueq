-- 0002_dal_smoke — throwaway table used only by Tools/Database/DAL Self-Test (1.2).
-- Proves the async DAL round-trips writes + reads. Dropped once 1.3's real tables exist
-- (or left as a harmless scratch table).
CREATE TABLE IF NOT EXISTS dal_smoke (
    id         TEXT        PRIMARY KEY,
    payload    TEXT        NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
