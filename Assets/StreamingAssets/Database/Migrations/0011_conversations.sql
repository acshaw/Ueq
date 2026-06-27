-- 0011_conversations — M2.4. NPC keyword conversation sets. Server-only at runtime (the whole
-- conversation state machine is server-side). A set is a named list of keywords; each keyword has a
-- response, a mode (0 passive / 1 active), flags, an optional faction gate, and a list of keywords it
-- unlocks for the player. Faction gate references a faction by id (factions land in DB at 2.6; until
-- then an unresolved gate is treated as ungated). Existing SO sets (Captain, GuardKeywords) are seeded.
CREATE TABLE IF NOT EXISTS conversation_sets (
    set_id       TEXT        PRIMARY KEY,
    display_name TEXT        NOT NULL DEFAULT '',
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS conversation_keywords (
    id                  BIGSERIAL PRIMARY KEY,
    set_id              TEXT      NOT NULL REFERENCES conversation_sets(set_id) ON DELETE CASCADE,
    sort_order          INT       NOT NULL DEFAULT 0,
    keyword             TEXT      NOT NULL,
    mode                INT       NOT NULL DEFAULT 0,   -- KeywordMode: 0 Passive, 1 Active
    is_opener           BOOLEAN   NOT NULL DEFAULT FALSE,
    ends_conversation   BOOLEAN   NOT NULL DEFAULT FALSE,
    requires_unlock     BOOLEAN   NOT NULL DEFAULT FALSE,
    response            TEXT      NOT NULL DEFAULT '',
    required_faction_id TEXT      NULL,                 -- references a faction (2.6); null = no gate
    required_standing   TEXT      NULL
);

CREATE TABLE IF NOT EXISTS conversation_keyword_unlocks (
    id               BIGSERIAL PRIMARY KEY,
    keyword_id       BIGINT NOT NULL REFERENCES conversation_keywords(id) ON DELETE CASCADE,
    unlocked_keyword TEXT   NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_conversation_keywords_set ON conversation_keywords(set_id);
CREATE INDEX IF NOT EXISTS ix_conversation_keyword_unlocks_kw ON conversation_keyword_unlocks(keyword_id);
