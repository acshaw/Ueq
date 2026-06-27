-- 0009_items — first real content type (M2.2). Mirrors ItemDefinition 1:1 (flat — items have
-- no child collections). item_id is the stable, human-readable key already referenced across the
-- game (inventory, equipment, loot, vendors), so it is the primary key (devplan D5).
-- Schema only; the existing Resources/Items rows are seeded idempotently by DatabaseSeeder, and
-- the web Item Editor owns all data edits thereafter (migrations own schema, API owns data).
CREATE TABLE IF NOT EXISTS items (
    item_id            TEXT        PRIMARY KEY,
    display_name       TEXT        NOT NULL DEFAULT '',
    description        TEXT        NOT NULL DEFAULT '',
    max_stack_size     INT         NOT NULL DEFAULT 1,

    is_equippable      BOOLEAN     NOT NULL DEFAULT FALSE,
    equip_slot         INT         NOT NULL DEFAULT 11,   -- EquipSlot enum (11 = Weapon)

    bonus_str          INT         NOT NULL DEFAULT 0,
    bonus_sta          INT         NOT NULL DEFAULT 0,
    bonus_agi          INT         NOT NULL DEFAULT 0,
    bonus_dex          INT         NOT NULL DEFAULT 0,
    bonus_int          INT         NOT NULL DEFAULT 0,
    bonus_wis          INT         NOT NULL DEFAULT 0,
    bonus_cha          INT         NOT NULL DEFAULT 0,

    weapon_base_damage INT         NOT NULL DEFAULT 10,
    weapon_delay       REAL        NOT NULL DEFAULT 2,
    weapon_range       REAL        NOT NULL DEFAULT 3,
    weapon_category    INT         NOT NULL DEFAULT 0,    -- WeaponCategory enum (0 = Might)

    buy_price          INT         NOT NULL DEFAULT 0,    -- copper; 0 = vendors don't sell
    sell_price         INT         NOT NULL DEFAULT 0,    -- copper; 0 = vendors won't buy

    icon_address       TEXT        NULL,                  -- Addressables address (D2); null = no icon

    updated_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);
