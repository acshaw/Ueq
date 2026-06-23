-- 0006_character_name_unique — globally-unique character names (M1 / 1.5, decision D4).
--
-- Partial + case-insensitive: uniqueness is enforced on lower(name), and only for rows that
-- actually have a name (WHERE name <> '') so 1.3's empty-name default rows don't collide.
-- The index is the real guard (the create flow also does a friendly pre-check for UX).
CREATE UNIQUE INDEX IF NOT EXISTS characters_name_unique_idx
    ON characters (lower(name))
    WHERE name <> '';
