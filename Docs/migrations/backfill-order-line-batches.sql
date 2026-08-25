-- Idempotent backfill script for order_line_batches
-- Creates one batch row per order_line that has lot_number populated
-- but no existing order_line_batches rows, for orders not shipped/cancelled.
-- Safe to run multiple times.

INSERT INTO order_line_batches (order_line_id, lot_number, expiry_date, quantity, packed_at, packed_by_user_id)
SELECT ol.id, ol.lot_number, ol.expiry_date, ol.quantity, ol.packed_at, ol.packed_by_user_id
FROM order_lines ol
JOIN orders o ON o.id = ol.order_id
WHERE o.status NOT IN ('shipped', 'cancelled')
  AND ol.lot_number IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM order_line_batches olb WHERE olb.order_line_id = ol.id
  );
