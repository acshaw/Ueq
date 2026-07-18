-- 2.9 — Abilities, tags & effects (DB-backed content type).
-- Header table + 3 ordered children: semantic tags, cooldown links, and the ordered effect list.
-- effect_type + a shared amount/scaling-stat/scaling-factor shape covers today's two effect types
-- (damage, heal); new effect types add columns in their own migration when actually built (AB1).

CREATE TABLE abilities (
  ability_id     TEXT PRIMARY KEY,
  display_name   TEXT NOT NULL,
  description    TEXT NOT NULL DEFAULT '',
  targeting_type INT  NOT NULL DEFAULT 1,   -- 0=Self, 1=SingleTarget
  range          REAL NOT NULL DEFAULT 20,
  cast_time      REAL NOT NULL DEFAULT 0,
  mana_cost      INT  NOT NULL DEFAULT 0,
  anim_trigger   TEXT NOT NULL DEFAULT '',
  updated_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE ability_tags (
  tag_id       TEXT PRIMARY KEY,
  display_name TEXT NOT NULL
);

CREATE TABLE ability_definition_tags (
  id         SERIAL PRIMARY KEY,
  ability_id TEXT NOT NULL REFERENCES abilities(ability_id) ON DELETE CASCADE,
  tag_id     TEXT NOT NULL REFERENCES ability_tags(tag_id),
  sort_order INT  NOT NULL DEFAULT 0
);

CREATE TABLE ability_cooldown_links (
  id         SERIAL PRIMARY KEY,
  ability_id TEXT NOT NULL REFERENCES abilities(ability_id) ON DELETE CASCADE,
  sort_order INT  NOT NULL DEFAULT 0,
  tag_id     TEXT NOT NULL REFERENCES ability_tags(tag_id),
  duration   REAL NOT NULL DEFAULT 3
);

CREATE TABLE ability_effects (
  id             SERIAL PRIMARY KEY,
  ability_id     TEXT NOT NULL REFERENCES abilities(ability_id) ON DELETE CASCADE,
  sort_order     INT  NOT NULL DEFAULT 0,
  effect_type    TEXT NOT NULL,             -- 'damage' | 'heal'
  base_amount    INT  NOT NULL DEFAULT 0,
  scaling_stat   INT  NOT NULL DEFAULT 0,    -- ScalingStatType enum (None/Str/Sta/Agi/Dex/Int/Wis/Cha)
  scaling_factor REAL NOT NULL DEFAULT 0
);
