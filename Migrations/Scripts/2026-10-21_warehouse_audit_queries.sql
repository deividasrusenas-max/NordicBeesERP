-- Warehouse integrity audit — READ-ONLY (SELECT only). Phase 1, B1..B12.
-- Column names taken from Models/WarehouseModule/*.cs ([Column] attributes). `DESCRIBE <table>` was NOT run
-- (dev DB unreachable, no mysql-prod MCP in the session) — run DESCRIBE on containers, delivery_lines, deliveries,
-- stock_movements, supplier_payments first and fix any name that differs.
-- PR-MD2610-011 was repaired by hand in prod; it is excluded from nothing here (so you can see it is now clean).

-- B1 containers: net <> gross - tare
SELECT id, container_code, container_type, gross_weight, tare_weight, net_weight, gross_weight - tare_weight AS expected_net, status
FROM containers WHERE ABS(net_weight - (gross_weight - tare_weight)) > 0.005;

-- B2a percentiles per container_type (MariaDB: PERCENTILE_CONT window function)
SELECT DISTINCT container_type,
  COUNT(*) OVER (PARTITION BY container_type) AS n,
  MIN(gross_weight) OVER (PARTITION BY container_type) AS min_gross,
  PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY gross_weight) OVER (PARTITION BY container_type) AS p50_gross,
  PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY gross_weight) OVER (PARTITION BY container_type) AS p99_gross,
  MAX(gross_weight) OVER (PARTITION BY container_type) AS max_gross,
  PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY tare_weight) OVER (PARTITION BY container_type) AS p50_tare,
  MAX(tare_weight) OVER (PARTITION BY container_type) AS max_tare,
  PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY net_weight) OVER (PARTITION BY container_type) AS p50_net,
  MAX(net_weight) OVER (PARTITION BY container_type) AS max_net
FROM containers;

-- B2b structurally bad rows (no range assumption needed)
SELECT id, container_code, container_type, gross_weight, tare_weight, net_weight, status
FROM containers WHERE net_weight <= 0 OR gross_weight <= 0 OR tare_weight < 0 OR tare_weight >= gross_weight;

-- B2c absurd rows — thresholds are PLACEHOLDERS, set them from B2a (BARREL > 400 kg is the brief's figure)
SET @max_barrel_gross = 400, @max_bucket_gross = 60;
SELECT id, container_code, container_type, gross_weight, tare_weight, net_weight, status
FROM containers
WHERE (container_type = 'BARREL' AND gross_weight > @max_barrel_gross)
   OR (container_type IN ('BUCKET','BUCKET_GROUP') AND gross_weight > @max_bucket_gross);

-- B3 IN movements (Delivery) whose quantity <> container net
SELECT m.id AS movement_id, m.container_id, c.container_code, m.quantity AS movement_qty, c.net_weight, m.quantity - c.net_weight AS diff
FROM stock_movements m JOIN containers c ON c.id = m.container_id
WHERE m.movement_type = 'IN' AND m.reference_type = 'Delivery' AND ABS(m.quantity - c.net_weight) > 0.005;

-- B4a containers with no IN movement
SELECT c.id, c.container_code, c.status FROM containers c
WHERE NOT EXISTS (SELECT 1 FROM stock_movements m WHERE m.container_id = c.id AND m.movement_type = 'IN');
-- B4b containers with duplicate IN movements
SELECT container_id, COUNT(*) AS in_rows, GROUP_CONCAT(id) AS movement_ids FROM stock_movements
WHERE movement_type = 'IN' GROUP BY container_id HAVING COUNT(*) > 1;
-- B4c movements pointing to missing containers
SELECT m.id, m.container_id, m.movement_type, m.quantity FROM stock_movements m
LEFT JOIN containers c ON c.id = m.container_id WHERE c.id IS NULL;

-- B5 per-container balance. TRANSFER rows move between warehouses and are NOT an OUT.
-- (No code path writes consumption/sale movements or status SOLD/TRANSFERRED — see report; movement types seen in code: IN, OUT, TRANSFER.)
SELECT c.id, c.container_code, c.status, c.net_weight,
  COALESCE(SUM(CASE WHEN m.movement_type = 'IN'  THEN m.quantity END), 0) AS qty_in,
  COALESCE(SUM(CASE WHEN m.movement_type = 'OUT' THEN m.quantity END), 0) AS qty_out,
  COALESCE(SUM(CASE WHEN m.movement_type = 'IN' THEN m.quantity WHEN m.movement_type = 'OUT' THEN -m.quantity END), 0) AS balance
FROM containers c LEFT JOIN stock_movements m ON m.container_id = c.id
GROUP BY c.id, c.container_code, c.status, c.net_weight
HAVING (c.status = 'IN_STOCK' AND balance <= 0)
    OR (c.status <> 'IN_STOCK' AND balance > 0)
    OR balance < 0;
SELECT movement_type, COUNT(*) FROM stock_movements GROUP BY movement_type;   -- which movement types actually exist
SELECT status, COUNT(*) FROM containers GROUP BY status;                       -- which statuses actually exist

-- B6 container.warehouse_id vs last movement's destination
SELECT c.id, c.container_code, c.warehouse_id AS container_wh, lm.id AS last_movement_id, lm.movement_type, lm.to_warehouse_id, lm.from_warehouse_id
FROM containers c
JOIN stock_movements lm ON lm.id = (SELECT MAX(m2.id) FROM stock_movements m2 WHERE m2.container_id = c.id)
WHERE c.status = 'IN_STOCK' AND lm.to_warehouse_id IS NOT NULL AND lm.to_warehouse_id <> c.warehouse_id;

-- B7 delivery_lines vs containers
SELECT l.id AS line_id, d.delivery_number, l.total_net_weight, s.sum_net, l.container_count, s.sum_qty, s.cnt
FROM delivery_lines l JOIN deliveries d ON d.id = l.delivery_id
JOIN (SELECT delivery_line_id, SUM(net_weight) AS sum_net, SUM(quantity) AS sum_qty, COUNT(*) AS cnt FROM containers GROUP BY delivery_line_id) s
  ON s.delivery_line_id = l.id
WHERE ABS(COALESCE(l.total_net_weight,0) - s.sum_net) > 0.005 OR l.container_count <> s.sum_qty OR l.container_count <> s.cnt;

-- B8 line_total vs net x price
SELECT l.id, d.delivery_number, l.total_net_weight, l.unit_price, l.line_total, ROUND(l.total_net_weight * l.unit_price, 2) AS expected
FROM delivery_lines l JOIN deliveries d ON d.id = l.delivery_id
WHERE l.unit_price IS NOT NULL AND ABS(COALESCE(l.line_total,0) - l.total_net_weight * l.unit_price) > 0.01;

-- B9 delivery totals vs lines
SELECT d.id, d.delivery_number, d.total_net_weight, s.sum_net, d.total_amount, s.sum_total
FROM deliveries d JOIN (SELECT delivery_id, SUM(COALESCE(total_net_weight,0)) AS sum_net, SUM(COALESCE(line_total,0)) AS sum_total FROM delivery_lines GROUP BY delivery_id) s ON s.delivery_id = d.id
WHERE ABS(d.total_net_weight - s.sum_net) > 0.005 OR ABS(d.total_amount - s.sum_total) > 0.01;

-- B10 status / paid_amount vs payments (rules of DeliveryService.UpdateDeliveryStatusAsync)
SELECT d.id, d.delivery_number, d.status, d.total_amount, d.paid_amount, COALESCE(p.paid,0) AS payments_sum,
  CASE WHEN d.total_amount > 0 AND COALESCE(p.paid,0) >= d.total_amount THEN 'PAID'
       WHEN d.total_amount > 0 AND COALESCE(p.paid,0) > 0 THEN 'PARTIAL_PAID'
       WHEN d.total_amount > 0 THEN 'PRICED' ELSE 'RECEIVED' END AS expected_status
FROM deliveries d LEFT JOIN (SELECT delivery_id, SUM(amount) AS paid FROM supplier_payments GROUP BY delivery_id) p ON p.delivery_id = d.id
HAVING status <> expected_status OR ABS(paid_amount - payments_sum) > 0.005;

-- B11 barrels owed/returned vs BARREL containers
SELECT d.id, d.delivery_number, d.need_return_barrels, d.barrels_owed, d.barrels_returned,
  (SELECT COUNT(*) FROM containers c JOIN delivery_lines l ON l.id = c.delivery_line_id WHERE l.delivery_id = d.id AND c.container_type = 'BARREL') AS barrel_containers
FROM deliveries d
WHERE d.barrels_returned > d.barrels_owed
   OR (d.need_return_barrels = 1 AND d.barrels_owed > (SELECT COUNT(*) FROM containers c JOIN delivery_lines l ON l.id = c.delivery_line_id WHERE l.delivery_id = d.id AND c.container_type = 'BARREL'))
   OR (d.need_return_barrels = 0 AND (d.barrels_owed > 0 OR d.barrels_returned > 0));

-- B12 orphans / duplicates
SELECT l.id, l.delivery_id FROM delivery_lines l LEFT JOIN deliveries d ON d.id = l.delivery_id WHERE d.id IS NULL;
SELECT c.id, c.container_code, c.delivery_line_id FROM containers c LEFT JOIN delivery_lines l ON l.id = c.delivery_line_id WHERE c.delivery_line_id IS NULL OR l.id IS NULL;
SELECT container_code, COUNT(*) FROM containers GROUP BY container_code HAVING COUNT(*) > 1;
-- B13a delivery lines with zero containers
SELECT l.id, d.delivery_number, l.total_net_weight FROM delivery_lines l JOIN deliveries d ON d.id = l.delivery_id
WHERE NOT EXISTS (SELECT 1 FROM containers c WHERE c.delivery_line_id = l.id);
-- B13b invoices issued from a delivery whose current totals differ from the invoice (stale-total propagation)
SELECT d.id, d.delivery_number, d.invoice_number, d.total_amount, i.subtotal_excl_vat
FROM deliveries d JOIN invoices i ON i.id = d.invoice_id WHERE ABS(d.total_amount - i.subtotal_excl_vat) > 0.01;
-- B13c signed deliveries whose weights changed after signature (updated_at newer than signed_at)
SELECT d.id, d.delivery_number, d.supplier_signed_at, MAX(c.updated_at) AS last_container_edit
FROM deliveries d JOIN delivery_lines l ON l.delivery_id = d.id JOIN containers c ON c.delivery_line_id = l.id
WHERE d.supplier_signed_at IS NOT NULL GROUP BY d.id, d.delivery_number, d.supplier_signed_at HAVING MAX(c.updated_at) > d.supplier_signed_at;
