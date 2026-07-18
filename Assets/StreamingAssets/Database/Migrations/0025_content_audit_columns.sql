-- 2.11 (SE4) — close a real inconsistency found while scoping content export/import: every other
-- content header table already has updated_at and stamps it on edit; these three (added by 2.9/2.10)
-- didn't. A full change-attribution audit log is deferred to 6.3 (real auth) — there's no "who" yet.

ALTER TABLE races       ADD COLUMN updated_at TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE classes      ADD COLUMN updated_at TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE ability_tags ADD COLUMN updated_at TIMESTAMPTZ NOT NULL DEFAULT now();
