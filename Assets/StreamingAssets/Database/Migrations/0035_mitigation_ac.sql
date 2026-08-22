-- 0035_mitigation_ac — Step 4 (Mitigation) implementation (2026-08-21): AC is the sole mitigation
-- lever, reduced via a diminishing-returns curve (MitigationCurve.cs / combat-math.ts), asymptoting
-- at 50% past AC 800 per the design doc's own "cannot create invulnerability" requirement (doc §5,
-- was previously "NAMED — UNDEFINED", no formula). Items: bonus_ac, equipment-only, no class/race
-- base (mirrors bonus_str etc). Mobs: ac, authored directly as a flat number — same AD3/AV3
-- "no stats to derive from" treatment already used for atk/avoidance_dodge/parry/riposte.
ALTER TABLE items ADD COLUMN IF NOT EXISTS bonus_ac INT NOT NULL DEFAULT 0;
ALTER TABLE mobs  ADD COLUMN IF NOT EXISTS ac REAL NOT NULL DEFAULT 0;
