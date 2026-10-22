-- Apply manually (dev, nordic_bees_erp_test, staging, then prod). Works on MySQL 8.0.
-- Verify BEFORE on each DB:  SHOW COLUMNS FROM invoices LIKE 'supply_date';   (expect empty)
-- Not re-runnable (plain ADD COLUMN, project rule nordicbees-migration-if-not-exists): check SHOW COLUMNS first; skip if present.
-- Verify AFTER:  SHOW COLUMNS FROM invoices LIKE 'supply_date';   expect date, NULL, default NULL
-- Existing rows keep NULL (= same as invoice_date). The agent does NOT run DDL.
ALTER TABLE invoices ADD COLUMN supply_date DATE NULL AFTER invoice_date;

-- EF history row (ProductVersion copied from the latest existing row)
INSERT IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261022100000_AddInvoiceSupplyDate', h.ProductVersion FROM __EFMigrationsHistory h ORDER BY h.MigrationId DESC LIMIT 1;

-- Down: ALTER TABLE invoices DROP COLUMN supply_date;
