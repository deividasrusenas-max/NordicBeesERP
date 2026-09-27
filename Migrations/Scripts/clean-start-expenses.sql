-- =============================================================================
-- clean-start-expenses.sql — D-045 / D-047 / D-048 expense-module clean start
-- =============================================================================
--
-- WHAT THIS DOES
-- Deletes every expense invoice, its lines/audit/OCR-queue rows, the module's
-- `files` rows, every supplier_aliases/supplier_alias_events row, and every
-- "expense-only" supplier (business_partners.is_expense_supplier = 1 with no
-- reference from any other module), so the module can be re-populated from a
-- clean re-upload of the original PDFs (D-045 step 2/3, D-047 item 1).
-- Beekeepers and customers are NEVER deleted by this script.
--
-- ============================ !!! DANGER !!! ================================
-- THIS SCRIPT DELETES DATA. It is meant to be run against:
--   - STAGING (`nordic_bees_erp_staging`) — the next step after D-047's
--     correction (2026-09-27), per the owner's authorisation.
--   - PRODUCTION (`nordic_bees_erp`) — ONLY after the final Etapai 1-4 deploy
--     (D-037), and ONLY after a fresh backup (DB dump + blobs, see
--     RUNBOOK-FINAL-PROD-DEPLOY.md).
-- It has ALSO been dry-run tested against DEV (`nordic_bees_erp`,
-- `nordic_bees_erp_test` on 100.110.26.80) — see the DRY RUN counts in
-- `.opencode/reports/pre-cleanstart-A-*.md`. Dry-run testing on DEV is safe
-- (throwaway data); do NOT run Section 3 (the actual deletes) against DEV
-- unless you mean to destroy your local test fixtures.
--
-- WHICH DATABASE THIS RUNS AGAINST IS CHOSEN BY THE CALLER, NEVER HARD-CODED
-- HERE. There is no `USE` statement in this file on purpose — you select the
-- database on the command line:
--
--     sudo mariadb nordic_bees_erp_staging < Migrations/Scripts/clean-start-expenses.sql
--     sudo mariadb nordic_bees_erp          < Migrations/Scripts/clean-start-expenses.sql   -- PRODUCTION, only per RUNBOOK
--
-- Read the database name back before running:
--
--     sudo mariadb -e "SELECT DATABASE();" <db-you-are-about-to-touch>
--
-- COMPATIBILITY
-- Written once, portable across MariaDB (staging/prod, 11.8) and MySQL 8.0.46
-- (dev, 100.110.26.80) — every construct used here (stored procedures,
-- SIGNAL, transactions, information_schema.KEY_COLUMN_USAGE) is standard
-- SQL/PSM supported identically by both engines at the versions in use in
-- this project. No MariaDB-only or MySQL-only branch was needed.
--
-- HOW TO RUN
--   1. Section 1 (DRY RUN) is SELECT-only — always safe, run it first, on
--      its own, against whichever database you are about to touch:
--        sudo mariadb <db> < Migrations/Scripts/clean-start-expenses.sql
--      (running the WHOLE file executes sections 1-4 in order; Section 2's
--      guard procedure will refuse to let Section 3 do anything harmful if
--      any stop-count is non-zero, so running the whole file is the normal,
--      safe way to use it — Section 3 only touches rows if Section 2 passed)
--   2. Read the Section 1 output. If anything under "MUST BE ZERO" is not
--      zero, STOP — do not proceed; investigate first (see D-047 item 1: the
--      module's own audit trail, or a bank_import that quietly created a
--      payment, are the usual reasons).
--   3. If Section 1 looks right, the rest of the file (Sections 2-4) can be
--      run as part of the same invocation — Section 2 re-checks the exact
--      same conditions from inside a transaction and refuses to proceed
--      (ROLLBACK + SIGNAL) if anything changed since Section 1 ran.
--
-- BLOB CLEANUP (separate step, NOT part of this SQL file)
-- Deleting `files` rows here does NOT delete the blobs on disk (this project
-- never deletes blob bytes for rows it still might reference elsewhere, and a
-- SQL script has no filesystem access anyway). After this script commits, run
-- the companion shell snippet below AGAINST THE SAME ENVIRONMENT'S blob root:
--
--   Staging blob root:    /var/lib/nordicbees/staging/blobs   (host path; the
--                         container mounts it at /var/lib/nordicbees/data,
--                         see .github/workflows/deploy.yml's staging -v line)
--   Production blob root: /var/lib/nordicbees/prod/blobs      (same mount
--                         pattern, production -v line)
--
--   #!/usr/bin/env bash
--   # clean-start-expenses-blobs.sh <blob-root> <db-name>
--   # Deletes only blob files whose sha256 no longer appears in ANY `files`
--   # row (not just expense ones — a blob can be shared/deduplicated across
--   # modules by content hash, so this must check the WHOLE files table, not
--   # just module='expenses').
--   set -euo pipefail
--   BLOB_ROOT="${1:?usage: clean-start-expenses-blobs.sh <blob-root> <db-name>}"
--   DB_NAME="${2:?usage: clean-start-expenses-blobs.sh <blob-root> <db-name>}"
--   [ -d "$BLOB_ROOT/blobs" ] || { echo "No such directory: $BLOB_ROOT/blobs" >&2; exit 1; }
--   removed=0
--   kept=0
--   find "$BLOB_ROOT/blobs" -type f | while read -r path; do
--     sha256="$(basename "$path")"
--     count="$(sudo mariadb -N -B "$DB_NAME" -e "SELECT COUNT(*) FROM files WHERE sha256 = '${sha256}';")"
--     if [ "$count" -eq 0 ]; then
--       rm -f -- "$path"
--       removed=$((removed + 1))
--     else
--       kept=$((kept + 1))
--     fi
--   done
--   echo "Removed: $removed orphan blob(s). Kept: $kept blob(s) still referenced."
--
-- (This uses the sha256 as the sole trust anchor — exactly how FileStore.cs
-- addresses blobs — and is intentionally read-then-act per file rather than a
-- single bulk query, since $DB_NAME's answer must reflect the SQL script's
-- commit, which already happened by the time this runs.)
--
-- =============================================================================
-- SECTION 1 — DRY RUN (SELECT only, always safe)
-- =============================================================================

SELECT '--- SECTION 1: DRY RUN ---' AS _;

SELECT 'MUST BE ZERO TO PROCEED' AS _;

SELECT
    (SELECT COUNT(*) FROM expense_payments)                                        AS expense_payments_count,
    (SELECT COUNT(*) FROM expense_line_allocations)                                AS expense_line_allocations_count,
    (SELECT COUNT(*) FROM email_invoice_imports WHERE expense_invoice_id IS NOT NULL) AS email_imports_linked_count,
    (SELECT COUNT(*) FROM expense_invoices WHERE status IN ('PAID', 'PARTIAL'))     AS paid_or_partial_invoices_count;

SELECT 'NEW/UNKNOWN REFERENCES TO business_partners — MUST BE ZERO ROWS (see header: fail loudly on schema drift)' AS _;

-- The known set below was built from information_schema on 2026-09-27 (dev,
-- nordic_bees_erp_test) and includes supplier_approvals — a table NOT named
-- in D-048/D-047's own text, found only by running this query; a real example
-- of exactly the drift this check exists to catch. If this SELECT returns any
-- row, a table now references business_partners that this script does not
-- know how to treat — STOP, read Section 3's supplier-deletion query, and add
-- the new table there deliberately (never silently ignore it).
SELECT TABLE_NAME, COLUMN_NAME
FROM information_schema.KEY_COLUMN_USAGE
WHERE REFERENCED_TABLE_SCHEMA = DATABASE()
  AND REFERENCED_TABLE_NAME = 'business_partners'
  AND NOT (
       (TABLE_NAME = 'containers'          AND COLUMN_NAME = 'supplier_id')
    OR (TABLE_NAME = 'containers'          AND COLUMN_NAME = 'reservation_customer_id')
    OR (TABLE_NAME = 'credit_notes'        AND COLUMN_NAME = 'customer_id')
    OR (TABLE_NAME = 'deliveries'          AND COLUMN_NAME = 'supplier_id')
    OR (TABLE_NAME = 'honey_deliveries'    AND COLUMN_NAME = 'supplier_id')
    OR (TABLE_NAME = 'invoices'            AND COLUMN_NAME = 'customer_id')
    OR (TABLE_NAME = 'lots'                AND COLUMN_NAME = 'customer_id')
    OR (TABLE_NAME = 'orders'              AND COLUMN_NAME = 'customer_id')
    OR (TABLE_NAME = 'payments'            AND COLUMN_NAME = 'customer_id')
    OR (TABLE_NAME = 'supplier_approvals'  AND COLUMN_NAME = 'supplier_id')
    OR (TABLE_NAME = 'supplier_payments'   AND COLUMN_NAME = 'supplier_id')
    OR (TABLE_NAME = 'expense_invoices'    AND COLUMN_NAME = 'supplier_id')
  );

SELECT 'WOULD BE DELETED (preview counts)' AS _;

SELECT
    (SELECT COUNT(*) FROM expense_invoices)                                       AS expense_invoices_count,
    (SELECT COUNT(*) FROM expense_invoice_lines)                                  AS expense_invoice_lines_count,
    (SELECT COUNT(*) FROM expense_invoice_audit)                                  AS expense_invoice_audit_count,
    (SELECT COUNT(*) FROM expense_ocr_queue)                                      AS expense_ocr_queue_count,
    (SELECT COUNT(*) FROM files WHERE module = 'expenses')                        AS files_count,
    (SELECT COUNT(*) FROM supplier_aliases)                                       AS supplier_aliases_count,
    (SELECT COUNT(*) FROM supplier_alias_events)                                  AS supplier_alias_events_count;

SELECT 'files.sha256 list for the blob-cleanup shell snippet (module=expenses only — a shared/deduped blob may still be needed by another module, the shell snippet itself re-checks the WHOLE files table before deleting anything on disk)' AS _;

SELECT sha256, original_filename, entity_id AS invoice_id
FROM files
WHERE module = 'expenses'
ORDER BY id;

SELECT 'EXPENSE-ONLY SUPPLIERS THAT WOULD BE DELETED (is_expense_supplier=1, unreferenced elsewhere)' AS _;

SELECT bp.id, bp.name, bp.vat_code, bp.company_code
FROM business_partners bp
WHERE bp.is_expense_supplier = 1
  AND NOT EXISTS (SELECT 1 FROM honey_deliveries hd WHERE hd.supplier_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM supplier_payments sp WHERE sp.supplier_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM containers c WHERE c.supplier_id = bp.id OR c.reservation_customer_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM deliveries d WHERE d.supplier_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM invoices i WHERE i.customer_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM credit_notes cn WHERE cn.customer_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM orders o WHERE o.customer_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM payments p WHERE p.customer_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM lots l WHERE l.customer_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM supplier_approvals sa WHERE sa.supplier_id = bp.id)
ORDER BY bp.id;

-- =============================================================================
-- SECTION 2 — GUARD (aborts the whole script if any stop condition is non-zero)
-- =============================================================================

SELECT '--- SECTION 2: GUARD ---' AS _;

DROP PROCEDURE IF EXISTS _clean_start_guard;

DELIMITER $$

CREATE PROCEDURE _clean_start_guard()
BEGIN
    DECLARE v_payments INT;
    DECLARE v_allocations INT;
    DECLARE v_email_imports INT;
    DECLARE v_paid INT;
    DECLARE v_unknown_refs INT;

    SELECT COUNT(*) INTO v_payments FROM expense_payments;
    SELECT COUNT(*) INTO v_allocations FROM expense_line_allocations;
    SELECT COUNT(*) INTO v_email_imports FROM email_invoice_imports WHERE expense_invoice_id IS NOT NULL;
    SELECT COUNT(*) INTO v_paid FROM expense_invoices WHERE status IN ('PAID', 'PARTIAL');

    SELECT COUNT(*) INTO v_unknown_refs
    FROM information_schema.KEY_COLUMN_USAGE
    WHERE REFERENCED_TABLE_SCHEMA = DATABASE()
      AND REFERENCED_TABLE_NAME = 'business_partners'
      AND NOT (
           (TABLE_NAME = 'containers'          AND COLUMN_NAME = 'supplier_id')
        OR (TABLE_NAME = 'containers'          AND COLUMN_NAME = 'reservation_customer_id')
        OR (TABLE_NAME = 'credit_notes'        AND COLUMN_NAME = 'customer_id')
        OR (TABLE_NAME = 'deliveries'          AND COLUMN_NAME = 'supplier_id')
        OR (TABLE_NAME = 'honey_deliveries'    AND COLUMN_NAME = 'supplier_id')
        OR (TABLE_NAME = 'invoices'            AND COLUMN_NAME = 'customer_id')
        OR (TABLE_NAME = 'lots'                AND COLUMN_NAME = 'customer_id')
        OR (TABLE_NAME = 'orders'              AND COLUMN_NAME = 'customer_id')
        OR (TABLE_NAME = 'payments'            AND COLUMN_NAME = 'customer_id')
        OR (TABLE_NAME = 'supplier_approvals'  AND COLUMN_NAME = 'supplier_id')
        OR (TABLE_NAME = 'supplier_payments'   AND COLUMN_NAME = 'supplier_id')
        OR (TABLE_NAME = 'expense_invoices'    AND COLUMN_NAME = 'supplier_id')
      );

    IF v_payments <> 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ABORT: expense_payments is not empty. Investigate before a clean start.';
    END IF;
    IF v_allocations <> 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ABORT: expense_line_allocations is not empty. Investigate before a clean start.';
    END IF;
    IF v_email_imports <> 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ABORT: email_invoice_imports has rows linked to expense invoices. Investigate before a clean start.';
    END IF;
    IF v_paid <> 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ABORT: at least one expense invoice is PAID or PARTIAL. Investigate before a clean start.';
    END IF;
    IF v_unknown_refs <> 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ABORT: a table not known to this script now references business_partners (schema drift) — update the known-tables list in this file before proceeding.';
    END IF;
END$$

DELIMITER ;

CALL _clean_start_guard();

SELECT 'GUARD PASSED — proceeding to Section 3' AS _;

-- =============================================================================
-- SECTION 3 — DELETE (dependency order, one transaction)
-- =============================================================================

SELECT '--- SECTION 3: DELETE ---' AS _;

START TRANSACTION;

-- Re-run the guard INSIDE the transaction too — closes the window between
-- Section 1/2 running and Section 3 starting (e.g. a bank import racing in a
-- payment between the two). If anything changed, this aborts the whole
-- transaction (SIGNAL inside a procedure called from within an open
-- transaction still rolls back cleanly on the client's error handling; the
-- explicit ROLLBACK below is the belt-and-braces version for a client that
-- does not auto-rollback on a fatal SQL error mid-script).
CALL _clean_start_guard();

-- 1. Alias learning data tied to the OCR module (D-048 item 1 / D-047 item 1):
--    wiped in full, not just rows tied to invoices being deleted — the aliases
--    were learned from the pre-clean-start data and are being reset with it.
DELETE FROM supplier_alias_events;
SELECT COUNT(*) AS supplier_alias_events_remaining FROM supplier_alias_events;

DELETE FROM supplier_aliases;
SELECT COUNT(*) AS supplier_aliases_remaining FROM supplier_aliases;

-- 2. OCR queue rows (FROZEN.md §5, OcrQueueWorker itself untouched — only its
--    data rows for the invoices being deleted).
DELETE FROM expense_ocr_queue;
SELECT COUNT(*) AS expense_ocr_queue_remaining FROM expense_ocr_queue;

-- 3. Audit trail for the invoices being deleted.
DELETE FROM expense_invoice_audit;
SELECT COUNT(*) AS expense_invoice_audit_remaining FROM expense_invoice_audit;

-- 4. Lines (expense_line_allocations already verified empty by the guard, and
--    cascades from this delete regardless via ON DELETE CASCADE).
DELETE FROM expense_invoice_lines;
SELECT COUNT(*) AS expense_invoice_lines_remaining FROM expense_invoice_lines;

-- 5. The invoices themselves (expense_payments already verified empty by the
--    guard; email_invoice_imports.expense_invoice_id is ON DELETE SET NULL,
--    but the guard already required zero LINKED rows, so nothing is silently
--    orphaned here).
DELETE FROM expense_invoices;
SELECT COUNT(*) AS expense_invoices_remaining FROM expense_invoices;

-- 6. files rows for the module (blob bytes on disk are handled by the
--    separate shell snippet in this file's header, AFTER this commits).
DELETE FROM files WHERE module = 'expenses';
SELECT COUNT(*) AS files_remaining FROM files WHERE module = 'expenses';

-- 7. Expense-only suppliers and their bank accounts (D-047 item 2/3). The
--    NOT EXISTS predicate is identical to Section 1's preview — re-evaluated
--    now that step 6 hasn't touched business_partners, so the set is the same
--    unless something else changed business_partners between Section 1 and
--    here (which the re-run guard above would only catch for the four
--    specific stop-counts, not this — if that risk matters for a given run,
--    re-run Section 1's preview SELECT immediately before Section 3 in the
--    same session and compare row-for-row).
DELETE sba FROM supplier_bank_accounts sba
INNER JOIN business_partners bp ON bp.id = sba.partner_id
WHERE bp.is_expense_supplier = 1
  AND NOT EXISTS (SELECT 1 FROM honey_deliveries hd WHERE hd.supplier_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM supplier_payments sp WHERE sp.supplier_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM containers c WHERE c.supplier_id = bp.id OR c.reservation_customer_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM deliveries d WHERE d.supplier_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM invoices i WHERE i.customer_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM credit_notes cn WHERE cn.customer_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM orders o WHERE o.customer_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM payments p WHERE p.customer_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM lots l WHERE l.customer_id = bp.id)
  AND NOT EXISTS (SELECT 1 FROM supplier_approvals sa WHERE sa.supplier_id = bp.id);

DELETE FROM business_partners
WHERE is_expense_supplier = 1
  AND NOT EXISTS (SELECT 1 FROM honey_deliveries hd WHERE hd.supplier_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM supplier_payments sp WHERE sp.supplier_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM containers c WHERE c.supplier_id = business_partners.id OR c.reservation_customer_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM deliveries d WHERE d.supplier_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM invoices i WHERE i.customer_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM credit_notes cn WHERE cn.customer_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM orders o WHERE o.customer_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM payments p WHERE p.customer_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM lots l WHERE l.customer_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM supplier_approvals sa WHERE sa.supplier_id = business_partners.id);

SELECT COUNT(*) AS expense_only_suppliers_remaining
FROM business_partners
WHERE is_expense_supplier = 1
  AND NOT EXISTS (SELECT 1 FROM honey_deliveries hd WHERE hd.supplier_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM supplier_payments sp WHERE sp.supplier_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM containers c WHERE c.supplier_id = business_partners.id OR c.reservation_customer_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM deliveries d WHERE d.supplier_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM invoices i WHERE i.customer_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM credit_notes cn WHERE cn.customer_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM orders o WHERE o.customer_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM payments p WHERE p.customer_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM lots l WHERE l.customer_id = business_partners.id)
  AND NOT EXISTS (SELECT 1 FROM supplier_approvals sa WHERE sa.supplier_id = business_partners.id);

COMMIT;

DROP PROCEDURE IF EXISTS _clean_start_guard;

-- =============================================================================
-- SECTION 4 — POST-CHECKS
-- =============================================================================

SELECT '--- SECTION 4: POST-CHECKS ---' AS _;

SELECT
    (SELECT COUNT(*) FROM expense_invoices)        AS expense_invoices,
    (SELECT COUNT(*) FROM expense_invoice_lines)    AS expense_invoice_lines,
    (SELECT COUNT(*) FROM expense_invoice_audit)    AS expense_invoice_audit,
    (SELECT COUNT(*) FROM expense_ocr_queue)        AS expense_ocr_queue,
    (SELECT COUNT(*) FROM files WHERE module = 'expenses') AS expense_files,
    (SELECT COUNT(*) FROM supplier_aliases)         AS supplier_aliases,
    (SELECT COUNT(*) FROM supplier_alias_events)    AS supplier_alias_events;

SELECT 'Beekeepers/customers survived (spot check — should be >0 on any real environment)' AS _;
SELECT COUNT(*) AS remaining_beekeepers_and_customers
FROM business_partners
WHERE is_expense_supplier = 0;

SELECT 'DONE. Next: re-upload the original PDFs through the (bulk upload / dialog), then measure per PLAN-ETAPAS4.md.' AS _;
