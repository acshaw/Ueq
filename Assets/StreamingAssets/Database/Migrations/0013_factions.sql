-- 0013_factions — M2.6. Factions, the single shared named-threshold ladder, NPC-to-NPC ally/hostile
-- relations, and race→faction starting scores. Server-only content (no client sync — only player score
-- numbers reach clients, via PlayerFactionScores). References by id: mobs.faction_id and
-- conversation_keywords.required_faction_id point at factions.faction_id.
--
-- Also completes the faction score re-key (devplan DF2): scores are now keyed by faction_id everywhere,
-- so the persistence column character_faction_scores.faction_name is renamed to faction_id and the
-- existing seeded dev rows are migrated. faction_name survives only as a display label.
--
-- Schema only; reference rows seeded idempotently by DatabaseSeeder.

-- The single shared standing ladder (DC4) — KOS … Ally, ordered low→high by sort_order.
CREATE TABLE IF NOT EXISTS faction_thresholds (
    name        TEXT PRIMARY KEY,         -- "Indifferent"
    min_score   INT  NOT NULL,            -- inclusive lower bound
    sort_order  INT  NOT NULL             -- low standing → high standing
);

-- A faction. faction_id = stable key (matches mobs.faction_id / required_faction_id);
-- faction_name = display label only (no longer a score key).
CREATE TABLE IF NOT EXISTS factions (
    faction_id    TEXT PRIMARY KEY,        -- "CityGuards"
    faction_name  TEXT NOT NULL DEFAULT '',-- "City Guards"
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- NPC-to-NPC ally/hostile lists (social aggro / guards-respond, future 3.4). other_faction_id is a
-- soft ref (not FK'd) so relation rows tolerate authoring order.
CREATE TABLE IF NOT EXISTS faction_relations (
    faction_id        TEXT NOT NULL REFERENCES factions(faction_id) ON DELETE CASCADE,
    other_faction_id  TEXT NOT NULL,
    relation          TEXT NOT NULL CHECK (relation IN ('ally','hostile')),
    PRIMARY KEY (faction_id, other_faction_id, relation)
);

-- Starting standing a race has with a faction (seeds new characters; substituted under illusion).
CREATE TABLE IF NOT EXISTS race_faction_defaults (
    race        TEXT NOT NULL,             -- "Troll"
    faction_id  TEXT NOT NULL REFERENCES factions(faction_id) ON DELETE CASCADE,
    score       INT  NOT NULL,
    PRIMARY KEY (race, faction_id)
);

-- Persistence re-key (DF2): faction scores now key by faction_id, not faction_name. This migration
-- runs exactly once (tracked by schema_version), so the non-idempotent RENAME is safe.
ALTER TABLE character_faction_scores RENAME COLUMN faction_name TO faction_id;
UPDATE character_faction_scores SET faction_id = 'CityGuards'   WHERE faction_id = 'City Guards';
UPDATE character_faction_scores SET faction_id = 'QeynosGuards' WHERE faction_id = 'Qeynos Guards';
-- 'Vermin' faction_name already equals its id — no row to migrate.
