-- 0005_character_state — persisted character state (M1 / 1.3).
--
-- One character per account in 1.3 (UNIQUE(account_id)); the UNIQUE constraint is
-- relaxed in 1.6 when multiple characters per account land. FK to accounts(account_id)
-- is why this migration MUST be numbered ABOVE 0004 — the runner applies pending files
-- in ascending version order, so on a fresh DB accounts (0004) must exist first.
--
-- Scalars live on `characters`; the variable-length collections live in child tables
-- (decision O2) FK'd to characters with ON DELETE CASCADE. The repository writes the
-- whole set atomically in one transaction (delete-all-for-character then insert).
CREATE TABLE IF NOT EXISTS characters (
    character_id    BIGSERIAL   PRIMARY KEY,
    account_id      BIGINT      NOT NULL UNIQUE REFERENCES accounts(account_id) ON DELETE CASCADE,
    name            TEXT        NOT NULL DEFAULT '',   -- populated by character creation in 1.5
    race_name       TEXT        NOT NULL,
    class_name      TEXT        NOT NULL,
    total_xp        INTEGER     NOT NULL DEFAULT 0,
    copper          INTEGER     NOT NULL DEFAULT 0,
    silver          INTEGER     NOT NULL DEFAULT 0,
    gold            INTEGER     NOT NULL DEFAULT 0,
    platinum        INTEGER     NOT NULL DEFAULT 0,
    current_health  INTEGER     NOT NULL DEFAULT 0,
    current_mana    INTEGER     NOT NULL DEFAULT 0,
    pos_x           REAL        NOT NULL DEFAULT 0,
    pos_y           REAL        NOT NULL DEFAULT 0,
    pos_z           REAL        NOT NULL DEFAULT 0,
    yaw             REAL        NOT NULL DEFAULT 0,
    bind_x          REAL        NOT NULL DEFAULT 0,
    bind_y          REAL        NOT NULL DEFAULT 0,
    bind_z          REAL        NOT NULL DEFAULT 0,
    actual_race     TEXT        NOT NULL DEFAULT '',
    apparent_race   TEXT        NOT NULL DEFAULT '',
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS character_inventory (
    character_id    BIGINT  NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    slot_index      INTEGER NOT NULL,                 -- 0..7
    item_id         TEXT    NOT NULL,
    quantity        INTEGER NOT NULL,
    PRIMARY KEY (character_id, slot_index)
);

CREATE TABLE IF NOT EXISTS character_equipment (
    character_id    BIGINT  NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    slot            INTEGER NOT NULL,                 -- EquipSlot enum index, 0..12
    item_id         TEXT    NOT NULL,
    PRIMARY KEY (character_id, slot)
);

CREATE TABLE IF NOT EXISTS character_faction_scores (
    character_id    BIGINT  NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    faction_name    TEXT    NOT NULL,
    score           INTEGER NOT NULL,
    PRIMARY KEY (character_id, faction_name)
);

CREATE TABLE IF NOT EXISTS character_hotbar (
    character_id    BIGINT  NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    slot_index      INTEGER NOT NULL,                 -- 0..7
    ability_id      TEXT    NOT NULL,
    PRIMARY KEY (character_id, slot_index)
);
