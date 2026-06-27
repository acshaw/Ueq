-- 0012_mobs — M2.5. Mirrors MobDefinition 1:1 (flat). References other content by id:
-- faction_id (factions arrive at 2.6), conversation_set_id (2.4), loot_table_id (2.7), vendor_id (2.3).
-- prefab_address names a registered Mirror spawnable prefab (most mobs share "Enemy"; resolved at load).
-- Existing SO mobs (Giant Rat, City Guard, Captain of the Guard, Merchant) are seeded by DatabaseSeeder.
CREATE TABLE IF NOT EXISTS mobs (
    mob_id               TEXT        PRIMARY KEY,
    display_name         TEXT        NOT NULL DEFAULT '',
    mob_level            INT         NOT NULL DEFAULT 1,
    prefab_address       TEXT        NULL,                 -- registered spawnable prefab name; null/empty = unspawnable

    max_health           INT         NOT NULL DEFAULT 10,
    attack_damage        INT         NOT NULL DEFAULT 1,
    attack_interval      REAL        NOT NULL DEFAULT 2,
    attack_range         REAL        NOT NULL DEFAULT 2,

    movement_type        INT         NOT NULL DEFAULT 1,   -- MovementType: 0 Stationary, 1 Wander
    move_speed           REAL        NOT NULL DEFAULT 3.5,
    wander_radius        REAL        NOT NULL DEFAULT 10,
    wander_pause_min     REAL        NOT NULL DEFAULT 2,
    wander_pause_max     REAL        NOT NULL DEFAULT 6,

    perception_radius    REAL        NOT NULL DEFAULT 20,
    base_aggro_threat    INT         NOT NULL DEFAULT 1,

    faction_id           TEXT        NULL,
    aggro_max_standing   TEXT        NOT NULL DEFAULT 'Threatening',
    warning_max_standing TEXT        NOT NULL DEFAULT 'Apprehensive',

    conversation_set_id  TEXT        NULL,
    loot_table_id        TEXT        NULL,
    xp_reward            INT         NOT NULL DEFAULT 0,

    vendor_id            TEXT        NULL,
    vendor_open_keyword  TEXT        NOT NULL DEFAULT 'wares',

    updated_at           TIMESTAMPTZ NOT NULL DEFAULT now()
);
