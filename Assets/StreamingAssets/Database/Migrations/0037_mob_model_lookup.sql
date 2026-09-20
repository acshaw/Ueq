-- 0037_mob_model_lookup — decouples a mob's body-art reference from its mob_id/slug, and adds a small
-- lookup table of known MobModelCatalog ids so the web Mob Editor can offer a dropdown instead of
-- requiring the exact catalog modelId string to be typed by hand. mob_models is populated by
-- Tools/Character/Sync Mob Model Catalog to Database (Unity Editor) and is read-only from the web API.
-- mobs.model_id is deliberately NOT a foreign key into mob_models — a mob referencing a model id that's
-- since been removed from the catalog should keep working (MobModel falls back to the mob_id convention
-- path client-side) rather than being blocked or having its reference cascade-deleted.
ALTER TABLE mobs ADD COLUMN IF NOT EXISTS model_id TEXT;

CREATE TABLE IF NOT EXISTS mob_models (
    model_id   TEXT        PRIMARY KEY,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
