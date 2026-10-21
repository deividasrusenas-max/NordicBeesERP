-- #8 — invoices.due_date was never written by the app (only payment_due_date). The code now reads
-- COALESCE(due_date, payment_due_date), so this backfill is optional (keeps the column honest for external reports).
-- NOT executed by the agent. Run manually on prod; review the SELECTs, then replace ROLLBACK with COMMIT.
START TRANSACTION;

SELECT COUNT(*) AS invoices_missing_due_date
FROM invoices WHERE due_date IS NULL AND payment_due_date IS NOT NULL;

UPDATE invoices
SET due_date = payment_due_date
WHERE due_date IS NULL AND payment_due_date IS NOT NULL;

SELECT COUNT(*) AS still_missing_due_date FROM invoices WHERE due_date IS NULL AND payment_due_date IS NOT NULL;

ROLLBACK; -- change to COMMIT only after checking the numbers above
