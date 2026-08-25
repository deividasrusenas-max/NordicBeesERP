-- Phase 0: Add quantity_shipped to order_shipment_pallets for partial shipment tracking
-- This migration adds support for quantity-based partial shipments within a single batch.
-- Run on dev/test first, then production.

-- 1. Add quantity_shipped column (MariaDB doesn't support IF NOT EXISTS for ADD COLUMN)
ALTER TABLE order_shipment_pallets
    ADD COLUMN quantity_shipped DECIMAL(10,3) NOT NULL DEFAULT 0;

-- 2. Backfill existing shipped rows: for rows that exist before this migration,
--    the entire batch quantity was considered shipped (old whole-batch mechanism).
--    We populate quantity_shipped = batch.quantity so SUM(quantity_shipped) correctly
--    reflects already-shipped batches.
--    This is idempotent: running again won't change rows that already have the correct value.
UPDATE order_shipment_pallets osp
INNER JOIN order_line_batches olb ON olb.id = osp.order_line_batch_id
SET osp.quantity_shipped = olb.quantity
WHERE osp.quantity_shipped = 0;
