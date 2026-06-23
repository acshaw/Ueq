-- 0004_accounts — login accounts (M1 / 1.4).
-- Parent table for player identity. 1.3's characters table will add a FK
-- characters.account_id -> accounts.account_id once it lands.
--
-- username is stored lower-cased and UNIQUE so registration races resolve at the
-- DB (INSERT ... ON CONFLICT (username) DO NOTHING). password_hash is a PHC-style
-- self-describing string "pbkdf2$<iterations>$<salt_b64>$<subkey_b64>" — never plaintext.
CREATE TABLE IF NOT EXISTS accounts (
    account_id     BIGSERIAL   PRIMARY KEY,
    username       TEXT        NOT NULL UNIQUE,
    password_hash  TEXT        NOT NULL,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_login_at  TIMESTAMPTZ
);

-- Retire the 1.2 throwaway DAL smoke table now that a real persisted table exists (decision DA3 / 1.3 O5).
DROP TABLE IF EXISTS dal_smoke;
