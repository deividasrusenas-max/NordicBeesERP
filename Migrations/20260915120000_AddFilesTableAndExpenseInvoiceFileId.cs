using Microsoft.EntityFrameworkCore.Migrations;

namespace NordicBeesERP.Migrations;

public partial class AddFilesTableAndExpenseInvoiceFileId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS files (
                id BIGINT NOT NULL AUTO_INCREMENT,
                sha256 CHAR(64) NOT NULL,
                byte_size BIGINT NOT NULL,
                mime_type VARCHAR(255) NULL,
                original_filename VARCHAR(255) NULL,
                module VARCHAR(50) NOT NULL,
                entity_type VARCHAR(50) NOT NULL,
                entity_id BIGINT NULL,
                created_at DATETIME NOT NULL,
                created_by VARCHAR(100) NULL,
                deleted_at DATETIME NULL,
                deleted_reason VARCHAR(500) NULL,
                PRIMARY KEY (id)
            ) ENGINE=InnoDB;

            -- Idempotent + portable on MySQL 8.0 AND MariaDB 11.8:
            -- MySQL 8.0 has no CREATE INDEX IF NOT EXISTS, so guard via information_schema.
            SET @ddl = IF(
                (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'files' AND INDEX_NAME = 'IX_files_sha256') = 0,
                'CREATE INDEX IX_files_sha256 ON files (sha256)',
                'SELECT 1');
            PREPARE stmt FROM @ddl;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;

            SET @ddl = IF(
                (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'files' AND INDEX_NAME = 'IX_files_module_entity') = 0,
                'CREATE INDEX IX_files_module_entity ON files (module, entity_type, entity_id)',
                'SELECT 1');
            PREPARE stmt FROM @ddl;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;

            -- MySQL 8.0 has no ADD COLUMN IF NOT EXISTS, so guard via information_schema.
            SET @ddl = IF(
                (SELECT COUNT(*) FROM information_schema.COLUMNS
                 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'expense_invoices' AND COLUMN_NAME = 'file_id') = 0,
                'ALTER TABLE expense_invoices ADD COLUMN file_id BIGINT NULL',
                'SELECT 1');
            PREPARE stmt FROM @ddl;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE expense_invoices
                DROP COLUMN IF EXISTS file_id;

            DROP TABLE IF EXISTS files;
        ");
    }
}
