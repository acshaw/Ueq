-- 5.11: web-editor admin accounts. Separate from the game's `accounts` table (a different
-- concern — these gate access to the content-authoring tool, not player characters). Exactly
-- two rows expected in practice (the user + their brother), created via a self-service
-- registration endpoint gated by a shared invite code, not player-facing signup.
CREATE TABLE IF NOT EXISTS web_admins (
    id            BIGSERIAL PRIMARY KEY,
    username      TEXT NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);
