-- 0019_item_lore — M3.2.1. LORE flag (EQ1-style): an item can be possessed at most once (inventory +
-- equipped). Enforced server-side in PlayerInventory on the external acquire paths (loot / vendor buy /
-- quest reward) — not baked into AddItem (internal equip/unequip moves must not be blocked). The anti-farm
-- lever for 3.2's repeatable item-reward quests. Additive + non-breaking: existing items default to false.
ALTER TABLE items ADD COLUMN IF NOT EXISTS lore BOOLEAN NOT NULL DEFAULT false;
