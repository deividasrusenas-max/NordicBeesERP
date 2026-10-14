-- Apply manually (dev, test, staging, then prod). Works on MariaDB 11.8 and MySQL 8.0. Re-runnable: MODIFY to the same definition is a no-op.
-- Verify BEFORE on each DB:  SHOW COLUMNS FROM invoices LIKE 'status';   (dev currently: enum('draft','confirmed','paid','disputed') NULL DEFAULT 'draft')
-- Superset only: the existing members keep their order and every existing row stays valid; 'cancelled' is appended.
-- The column stays NULLable (as it is today) - do not add NOT NULL here.
-- Verify AFTER:  SHOW COLUMNS FROM invoices LIKE 'status';   expect enum('draft','confirmed','paid','disputed','cancelled')
-- The agent does NOT run DDL.
ALTER TABLE invoices MODIFY status ENUM('draft','confirmed','paid','disputed','cancelled') DEFAULT 'draft';

-- EF history row (ProductVersion copied from the latest existing row)
INSERT IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261015100000_InvoiceStatusAcceptsCancelled', h.ProductVersion FROM __EFMigrationsHistory h ORDER BY h.MigrationId DESC LIMIT 1;

-- Down: intentionally none (narrowing the enum would fail/truncate on rows holding 'cancelled').
