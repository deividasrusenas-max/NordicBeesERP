DELIMITER $$

-- Check and add pending_supplier_company_code
SET @dbname = DATABASE();
SET @tablename = "expense_invoices";
SET @columnname = "pending_supplier_company_code";
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE
      (
        table_name = @tablename
        AND table_schema = @dbname
        AND column_name = @columnname
      )
  ) > 0,
  "SELECT 1",
  CONCAT("ALTER TABLE ", @tablename, " ADD COLUMN ", @columnname, " VARCHAR(50) NULL")
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE @preparedStatement;

-- Check and add pending_supplier_bank_account
SET @dbname = DATABASE();
SET @tablename = "expense_invoices";
SET @columnname = "pending_supplier_bank_account";
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE
      (
        table_name = @tablename
        AND table_schema = @dbname
        AND column_name = @columnname
      )
  ) > 0,
  "SELECT 1",
  CONCAT("ALTER TABLE ", @tablename, " ADD COLUMN ", @columnname, " VARCHAR(100) NULL")
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE @preparedStatement;

-- Check and add pending_supplier_city
SET @dbname = DATABASE();
SET @tablename = "expense_invoices";
SET @columnname = "pending_supplier_city";
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE
      (
        table_name = @tablename
        AND table_schema = @dbname
        AND column_name = @columnname
      )
  ) > 0,
  "SELECT 1",
  CONCAT("ALTER TABLE ", @tablename, " ADD COLUMN ", @columnname, " VARCHAR(100) NULL")
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE @preparedStatement;

-- Check and add pending_supplier_postal_code
SET @dbname = DATABASE();
SET @tablename = "expense_invoices";
SET @columnname = "pending_supplier_postal_code";
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE
      (
        table_name = @tablename
        AND table_schema = @dbname
        AND column_name = @columnname
      )
  ) > 0,
  "SELECT 1",
  CONCAT("ALTER TABLE ", @tablename, " ADD COLUMN ", @columnname, " VARCHAR(20) NULL")
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE @preparedStatement;

-- Check and add pending_supplier_country_code
SET @dbname = DATABASE();
SET @tablename = "expense_invoices";
SET @columnname = "pending_supplier_country_code";
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE
      (
        table_name = @tablename
        AND table_schema = @dbname
        AND column_name = @columnname
      )
  ) > 0,
  "SELECT 1",
  CONCAT("ALTER TABLE ", @tablename, " ADD COLUMN ", @columnname, " VARCHAR(10) NULL")
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE @preparedStatement;

DELIMITER ;