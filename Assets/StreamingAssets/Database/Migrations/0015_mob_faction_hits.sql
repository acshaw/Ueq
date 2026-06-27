-- 0015_mob_faction_hits — M2.7.1. A faction adjustment applied to the killing player when a mob dies.
-- delta < 0 lowers the killer's standing with faction_id, > 0 raises it. faction_id is a soft ref to
-- factions.faction_id. Authored per mob (next to xp_reward); applied by MobKillReward via ModifyScore.
-- Schema only; demo rows seeded idempotently by DatabaseSeeder.
CREATE TABLE IF NOT EXISTS mob_faction_hits (
    id          BIGSERIAL PRIMARY KEY,
    mob_id      TEXT NOT NULL REFERENCES mobs(mob_id) ON DELETE CASCADE,
    faction_id  TEXT NOT NULL,
    delta       INT  NOT NULL,
    sort_order  INT  NOT NULL DEFAULT 0
);
