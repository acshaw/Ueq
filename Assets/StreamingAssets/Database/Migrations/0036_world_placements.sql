-- 0036_world_placements — Stage A of 2.7.3 (world placement sync). One generic table for every
-- physically-placed marker type (SpawnPoint, PatrolRoute, WanderRegion, and any future type) — a
-- brand-new marker type needs zero schema changes, it just defines its own `data` JSON shape.
-- placement_id is assigned once in the Unity Editor (a GUID baked into the scene) and is the stable
-- key used everywhere; zone_id is resolved from the containing scene, never hand-authored.
CREATE TABLE IF NOT EXISTS world_placements (
    placement_id UUID        PRIMARY KEY,
    zone_id      TEXT        NOT NULL,
    marker_type  TEXT        NOT NULL,
    pos_x        REAL,
    pos_y        REAL,
    pos_z        REAL,
    rot_y        REAL        NOT NULL DEFAULT 0,
    data         JSONB       NOT NULL DEFAULT '{}',
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_world_placements_zone ON world_placements(zone_id);
