-- 0020_conversation_rewards — M3.2. A quest turn-in/reward bundle on a conversation keyword. Saying a
-- faction-gated keyword to an NPC accepts required items/coin and grants XP/coin/items/faction — all-or-
-- nothing, and REPEATABLE (no completion tracking; anti-farm is the item LORE flag, 3.2.1 / Q2). Additive
-- + non-breaking: existing keywords carry no bundle (all zero / empty), so they behave exactly as before.
ALTER TABLE conversation_keywords ADD COLUMN IF NOT EXISTS reward_xp       INT NOT NULL DEFAULT 0;
ALTER TABLE conversation_keywords ADD COLUMN IF NOT EXISTS reward_copper   INT NOT NULL DEFAULT 0;
ALTER TABLE conversation_keywords ADD COLUMN IF NOT EXISTS required_copper INT NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS conversation_keyword_reward_items (
    id         BIGSERIAL PRIMARY KEY,
    keyword_id BIGINT NOT NULL REFERENCES conversation_keywords(id) ON DELETE CASCADE,
    item_id    TEXT   NOT NULL,
    quantity   INT    NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS conversation_keyword_required_items (
    id         BIGSERIAL PRIMARY KEY,
    keyword_id BIGINT NOT NULL REFERENCES conversation_keywords(id) ON DELETE CASCADE,
    item_id    TEXT   NOT NULL,
    quantity   INT    NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS conversation_keyword_faction_hits (
    id         BIGSERIAL PRIMARY KEY,
    keyword_id BIGINT NOT NULL REFERENCES conversation_keywords(id) ON DELETE CASCADE,
    faction_id TEXT   NOT NULL,
    delta      INT    NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_ckw_reward_items_kw   ON conversation_keyword_reward_items(keyword_id);
CREATE INDEX IF NOT EXISTS ix_ckw_required_items_kw ON conversation_keyword_required_items(keyword_id);
CREATE INDEX IF NOT EXISTS ix_ckw_faction_hits_kw   ON conversation_keyword_faction_hits(keyword_id);
