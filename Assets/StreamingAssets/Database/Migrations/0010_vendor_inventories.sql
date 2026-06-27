-- 0010_vendor_inventories — M2.3, first vertical of the NPC content cluster.
-- A vendor inventory is just an ordered list of item ids the vendor sells; prices come from the
-- referenced items (2.2). Server-only at runtime (shop validation + stock pushed to the client on
-- shop-open). No seed — Resources/Vendors was empty, so vendors are authored fresh in the web editor.
CREATE TABLE IF NOT EXISTS vendor_inventories (
    vendor_id    TEXT        PRIMARY KEY,
    display_name TEXT        NOT NULL DEFAULT '',
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS vendor_inventory_items (
    id         BIGSERIAL PRIMARY KEY,
    vendor_id  TEXT      NOT NULL REFERENCES vendor_inventories(vendor_id) ON DELETE CASCADE,
    item_id    TEXT      NOT NULL,
    sort_order INT       NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_vendor_inventory_items_vendor ON vendor_inventory_items(vendor_id);
