-- 2.10 — Races & classes (DB-backed content type). Flat header tables (mirrors the already-wide `mobs`
-- table for classes) + one child list (starting abilities, FK'd to 2.9's `abilities`). Weapon-prop
-- cosmetic fields (RC4) deliberately do NOT move to the DB — they stay Unity-asset wiring on the
-- CharacterRoster asset, since they're the one field with no numeric/text "content" value.

CREATE TABLE races (
  race_id      TEXT PRIMARY KEY,
  race_name    TEXT NOT NULL,
  xp_modifier  REAL NOT NULL DEFAULT 1,
  str_mod INT NOT NULL DEFAULT 0, sta_mod INT NOT NULL DEFAULT 0, agi_mod INT NOT NULL DEFAULT 0,
  dex_mod INT NOT NULL DEFAULT 0, int_mod INT NOT NULL DEFAULT 0, wis_mod INT NOT NULL DEFAULT 0,
  cha_mod INT NOT NULL DEFAULT 0
);

CREATE TABLE classes (
  class_id        TEXT PRIMARY KEY,
  class_name      TEXT NOT NULL,
  xp_modifier     REAL NOT NULL DEFAULT 1,

  base_str INT NOT NULL DEFAULT 10, base_sta INT NOT NULL DEFAULT 10, base_agi INT NOT NULL DEFAULT 10,
  base_dex INT NOT NULL DEFAULT 10, base_int INT NOT NULL DEFAULT 10, base_wis INT NOT NULL DEFAULT 10,
  base_cha INT NOT NULL DEFAULT 10,

  class_base_hp INT NOT NULL DEFAULT 15, hp_per_level INT NOT NULL DEFAULT 4,
  sta_cap INT NOT NULL DEFAULT 255, base_sta_ratio REAL NOT NULL DEFAULT 0.23, sta_growth_rate REAL NOT NULL DEFAULT 0.15,

  mana_stat_type INT NOT NULL DEFAULT 0, -- ManaStatType: 0 None, 1 Intellect, 2 Wisdom
  class_base_mana INT NOT NULL DEFAULT 0, mana_per_level INT NOT NULL DEFAULT 0, mana_cap INT NOT NULL DEFAULT 0,
  base_mana_ratio REAL NOT NULL DEFAULT 0.23, mana_growth_rate REAL NOT NULL DEFAULT 0,

  tier_l1_miss REAL NOT NULL DEFAULT 17.5, tier_l1_glancing REAL NOT NULL DEFAULT 40, tier_l1_hit REAL NOT NULL DEFAULT 30,
  tier_l1_solid REAL NOT NULL DEFAULT 10, tier_l1_good REAL NOT NULL DEFAULT 2.5,
  tier_l1_critical REAL NOT NULL DEFAULT 0, tier_l1_crippling REAL NOT NULL DEFAULT 0,

  tier_l20_miss REAL NOT NULL DEFAULT 2, tier_l20_glancing REAL NOT NULL DEFAULT 13, tier_l20_hit REAL NOT NULL DEFAULT 20,
  tier_l20_solid REAL NOT NULL DEFAULT 35, tier_l20_good REAL NOT NULL DEFAULT 25,
  tier_l20_critical REAL NOT NULL DEFAULT 3, tier_l20_crippling REAL NOT NULL DEFAULT 2
);

CREATE TABLE class_starting_abilities (
  id         SERIAL PRIMARY KEY,
  class_id   TEXT NOT NULL REFERENCES classes(class_id) ON DELETE CASCADE,
  ability_id TEXT NOT NULL REFERENCES abilities(ability_id),
  sort_order INT  NOT NULL DEFAULT 0
);
