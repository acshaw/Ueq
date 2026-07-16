-- 0021_weapon_skills — 2.12. Two persisted weapon-proficiency values per character (Might/Finesse),
-- earned through use (unlike the derived RPG stats, this genuinely needs to survive relog). Feeds the
-- combat pipeline's Skill Differential modifier (5.1.1).
ALTER TABLE characters ADD COLUMN IF NOT EXISTS might_skill   INT NOT NULL DEFAULT 0;
ALTER TABLE characters ADD COLUMN IF NOT EXISTS finesse_skill INT NOT NULL DEFAULT 0;
