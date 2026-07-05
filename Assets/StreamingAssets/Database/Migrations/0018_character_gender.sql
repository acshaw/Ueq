-- 0018_character_gender — M3.1.4. Gender becomes a first-class character attribute: picked first at
-- creation, it gates the available races/classes and (with race + class) selects the body model. Also
-- finally gives the conversation system's <gender> token a real value. Additive + non-breaking:
-- existing characters default to 'Male'.
ALTER TABLE characters ADD COLUMN IF NOT EXISTS gender TEXT NOT NULL DEFAULT 'Male';
