-- OFFSET-01 (užskaita, phase 1). Apply manually: dev, nordic_bees_erp_test, staging, then prod. MySQL 8.0. The agent does NOT run DDL.
-- Idempotent: CREATE TABLE IF NOT EXISTS + ENUM MODIFY (safe to repeat) + INSERT IGNORE history row.
-- Verify BEFORE on each DB:
--   SHOW CREATE TABLE payments;           -- payment_method / source enum values
--   SHOW CREATE TABLE expense_payments;   -- payment_method enum('BANK','CASH','OTHER'); source is varchar(20) (no change)
--   SHOW TABLES LIKE 'settlement_offsets'; -- expect empty on a first run
-- Verify AFTER:
--   SHOW CREATE TABLE settlement_offsets; SHOW CREATE TABLE settlement_offset_lines;
--   SHOW COLUMNS FROM payments LIKE 'payment_method';  -- must contain 'offset'
--   SHOW COLUMNS FROM payments LIKE 'source';          -- must contain 'offset'
--   SHOW COLUMNS FROM expense_payments LIKE 'payment_method'; -- must contain 'OFFSET'

CREATE TABLE IF NOT EXISTS settlement_offsets (
  id INT NOT NULL AUTO_INCREMENT,
  offset_number VARCHAR(30) NOT NULL,
  offset_date DATE NOT NULL,
  partner_id INT NOT NULL,
  currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
  amount DECIMAL(12,2) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'confirmed',
  notes TEXT NULL,
  created_by INT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  confirmed_by INT NULL,
  confirmed_at DATETIME NULL,
  cancelled_by INT NULL,
  cancelled_at DATETIME NULL,
  cancel_reason TEXT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_settlement_offsets_number (offset_number),
  KEY ix_settlement_offsets_partner (partner_id),
  CONSTRAINT fk_settlement_offsets_partner FOREIGN KEY (partner_id) REFERENCES business_partners (id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS settlement_offset_lines (
  id INT NOT NULL AUTO_INCREMENT,
  offset_id INT NOT NULL,
  side VARCHAR(20) NOT NULL,
  invoice_id INT NULL,
  expense_invoice_id INT NULL,
  amount DECIMAL(12,2) NOT NULL,
  open_before DECIMAL(12,2) NOT NULL,
  payment_id INT NULL,
  expense_payment_id INT NULL,
  PRIMARY KEY (id),
  KEY ix_settlement_offset_lines_offset (offset_id),
  CONSTRAINT fk_settlement_offset_lines_offset FOREIGN KEY (offset_id) REFERENCES settlement_offsets (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE payments
  MODIFY payment_method ENUM('bank_transfer','cash','card','other','offset') COLLATE utf8mb4_unicode_ci DEFAULT 'bank_transfer',
  MODIFY source ENUM('manual','bank_import','offset') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'manual';

ALTER TABLE expense_payments
  MODIFY payment_method ENUM('BANK','CASH','OTHER','OFFSET') COLLATE utf8mb4_unicode_ci DEFAULT 'BANK';

-- EF history row (ProductVersion copied from the latest existing row)
INSERT IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion)
SELECT '20261024100000_AddSettlementOffsets', h.ProductVersion FROM __EFMigrationsHistory h ORDER BY h.MigrationId DESC LIMIT 1;

-- Down (only while no offset rows exist):
--   DROP TABLE settlement_offset_lines; DROP TABLE settlement_offsets;
--   ALTER TABLE payments MODIFY payment_method ENUM('bank_transfer','cash','card','other') DEFAULT 'bank_transfer', MODIFY source ENUM('manual','bank_import') NOT NULL DEFAULT 'manual';
--   ALTER TABLE expense_payments MODIFY payment_method ENUM('BANK','CASH','OTHER') DEFAULT 'BANK';
