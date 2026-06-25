-- 0008_content_ping — throwaway smoke table for the 2.1 content platform foundation.
-- Proves the full Angular → .NET API → Postgres → Unity ContentLoader chain end to end
-- before any real content type (items = 2.2) rides on the rails. Dropped/repurposed once
-- 2.2 lands. Authored in the Angular editor, read by Unity's ContentLoader at host start.
CREATE TABLE IF NOT EXISTS content_ping (
    id         BIGSERIAL   PRIMARY KEY,
    label      TEXT        NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
