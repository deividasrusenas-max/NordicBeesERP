-- =====================================================================
-- 2026-10-22  Pre-2026 imported invoices -> paid
-- Deividas: all invoices dated before 2026-01-01 were imported and are paid;
-- payments were never entered. Without payment rows the debt reports show
-- ~685k EUR phantom debt (2022-2024) and any recalculation
-- (RecalculateInvoiceStatusInternalAsync: paid_amount = SUM(allocations))
-- would zero 2025 invoices too.
--
-- What it does, ONLY for invoices with invoice_date < 2026-01-01,
-- not KLA (credit invoices), not draft/cancelled, total > 0:
--   outstanding = total_incl_vat - SUM(payment_allocations) - credited (credit_notes printed/issued)
--   if outstanding > 0: insert one payment (method 'other', source 'manual',
--   reference_number 'IMPORT-PRE2026', bank_confirmed 1, date = invoice_date)
--   + one allocation for that amount; then invoice paid_amount = SUM(allocations),
--   payment_status 'paid', status 'paid', last_payment_date = latest allocation.
-- Idempotent: a second run finds nothing (outstanding = 0 / marker exists).
-- 2026 invoices are NOT touched.
--
-- Run:  sudo mariadb nordic_bees_erp < 2026-10-22_pre2026_invoices_paid.sql
-- Ends with ROLLBACK. Check the BEFORE/AFTER output, then change the last
-- line to COMMIT and run again.
-- =====================================================================

-- 0. structure check (must show the columns used below)
DESCRIBE payments;
DESCRIBE payment_allocations;

START TRANSACTION;

SET @marker := 'IMPORT-PRE2026';
SET @cutoff := '2026-01-01';

-- 1. BEFORE: per year / series
SELECT 'BEFORE' AS phase, YEAR(i.invoice_date) AS metai,
       LEFT(i.invoice_number, IF(i.invoice_number LIKE 'ULAK%', 4, 3)) AS serija,
       i.status, i.payment_status, COUNT(*) AS kiek,
       SUM(i.total_incl_vat) AS suma, SUM(i.paid_amount) AS paid_amount,
       SUM(COALESCE(a.alloc, 0)) AS per_mokejimus
FROM invoices i
LEFT JOIN (SELECT invoice_id, SUM(allocated_amount) AS alloc FROM payment_allocations GROUP BY invoice_id) a
       ON a.invoice_id = i.id
WHERE i.invoice_date < @cutoff
GROUP BY metai, serija, i.status, i.payment_status
ORDER BY metai, serija;

-- 2. invoices to settle
DROP TEMPORARY TABLE IF EXISTS tmp_pre2026;
CREATE TEMPORARY TABLE tmp_pre2026 AS
SELECT i.id AS invoice_id, i.customer_id, i.invoice_date, i.invoice_number,
       ROUND(i.total_incl_vat - COALESCE(a.alloc, 0) - COALESCE(c.credited, 0), 2) AS amount
FROM invoices i
LEFT JOIN (SELECT invoice_id, SUM(allocated_amount) AS alloc
           FROM payment_allocations GROUP BY invoice_id) a ON a.invoice_id = i.id
LEFT JOIN (SELECT original_invoice_id, ABS(SUM(total_incl_vat)) AS credited
           FROM credit_notes
           WHERE original_invoice_id IS NOT NULL AND status IN ('printed', 'issued')
           GROUP BY original_invoice_id) c ON c.original_invoice_id = i.id
WHERE i.invoice_date < @cutoff
  AND i.invoice_number NOT LIKE 'KLA%'
  AND LOWER(i.status) NOT IN ('draft', 'cancelled')
  AND i.total_incl_vat > 0
  AND NOT EXISTS (SELECT 1 FROM payments p WHERE p.invoice_id = i.id AND p.reference_number = @marker)
HAVING amount > 0.009;

SELECT 'TO SETTLE' AS phase, YEAR(invoice_date) AS metai,
       LEFT(invoice_number, IF(invoice_number LIKE 'ULAK%', 4, 3)) AS serija,
       COUNT(*) AS kiek, SUM(amount) AS suma
FROM tmp_pre2026 GROUP BY metai, serija ORDER BY metai, serija;

-- 3. payments + allocations
INSERT INTO payments
    (payment_date, invoice_id, customer_id, amount, payment_method, source,
     bank_confirmed, reference_number, notes, created_at, updated_at)
SELECT t.invoice_date, t.invoice_id, t.customer_id, t.amount, 'other', 'manual',
       1, @marker,
       'Importuota saskaita iki 2026-01-01: apmoketa, mokejimas nebuvo ivestas (migracija 2026-10-22)',
       NOW(), NOW()
FROM tmp_pre2026 t;

INSERT INTO payment_allocations (payment_id, invoice_id, allocated_amount, allocated_at)
SELECT p.id, p.invoice_id, p.amount, p.payment_date
FROM payments p
JOIN tmp_pre2026 t ON t.invoice_id = p.invoice_id
WHERE p.reference_number = @marker
  AND NOT EXISTS (SELECT 1 FROM payment_allocations pa WHERE pa.payment_id = p.id);

-- 4. invoice header = same result RecalculateInvoiceStatusInternalAsync would produce
UPDATE invoices i
JOIN tmp_pre2026 t ON t.invoice_id = i.id
JOIN (SELECT invoice_id, SUM(allocated_amount) AS s, MAX(allocated_at) AS d
      FROM payment_allocations GROUP BY invoice_id) a ON a.invoice_id = i.id
SET i.paid_amount = a.s,
    i.payment_status = 'paid',
    i.status = 'paid',
    i.last_payment_date = a.d,
    i.updated_at = NOW();

-- 5. AFTER
SELECT 'AFTER' AS phase, YEAR(i.invoice_date) AS metai,
       LEFT(i.invoice_number, IF(i.invoice_number LIKE 'ULAK%', 4, 3)) AS serija,
       i.status, i.payment_status, COUNT(*) AS kiek,
       SUM(i.total_incl_vat) AS suma, SUM(i.paid_amount) AS paid_amount,
       SUM(COALESCE(a.alloc, 0)) AS per_mokejimus
FROM invoices i
LEFT JOIN (SELECT invoice_id, SUM(allocated_amount) AS alloc FROM payment_allocations GROUP BY invoice_id) a
       ON a.invoice_id = i.id
WHERE i.invoice_date < @cutoff
GROUP BY metai, serija, i.status, i.payment_status
ORDER BY metai, serija;

-- must be 0 rows: pre-2026 invoices still not fully settled
SELECT 'STILL OPEN (expect 0)' AS phase, i.invoice_number, i.invoice_date, i.total_incl_vat,
       COALESCE(a.alloc, 0) AS alloc, COALESCE(c.credited, 0) AS credited
FROM invoices i
LEFT JOIN (SELECT invoice_id, SUM(allocated_amount) AS alloc FROM payment_allocations GROUP BY invoice_id) a ON a.invoice_id = i.id
LEFT JOIN (SELECT original_invoice_id, ABS(SUM(total_incl_vat)) AS credited FROM credit_notes
           WHERE original_invoice_id IS NOT NULL AND status IN ('printed', 'issued')
           GROUP BY original_invoice_id) c ON c.original_invoice_id = i.id
WHERE i.invoice_date < @cutoff
  AND i.invoice_number NOT LIKE 'KLA%'
  AND LOWER(i.status) NOT IN ('draft', 'cancelled')
  AND i.total_incl_vat > 0
  AND i.total_incl_vat - COALESCE(a.alloc, 0) - COALESCE(c.credited, 0) > 0.009;

-- 2026 invoices must be unchanged: compare with the earlier report (125 LAK + 13 ULAK rows)
SELECT 'CHECK 2026 untouched' AS phase, COUNT(*) AS changed_2026
FROM invoices WHERE invoice_date >= @cutoff AND updated_at >= NOW() - INTERVAL 1 MINUTE;

ROLLBACK;
-- COMMIT;
