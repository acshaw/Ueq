-- 0007_multichar — allow multiple characters per account (M1 / 1.6, decision O1/O2).
--
-- 1.3/1.5 enforced one character per account via a UNIQUE(account_id) constraint
-- (auto-named characters_account_id_key). Drop it so an account can hold several
-- characters; saves/loads now key off character_id (the PK), not account_id.
-- Keep the FK (characters_account_id_fkey) and add a plain index for list lookups.
ALTER TABLE characters DROP CONSTRAINT IF EXISTS characters_account_id_key;
CREATE INDEX IF NOT EXISTS characters_account_id_idx ON characters (account_id);
