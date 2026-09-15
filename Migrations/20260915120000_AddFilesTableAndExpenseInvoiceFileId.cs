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

            CREATE INDEX IX_files_sha256 ON files (sha256);
            CREATE INDEX IX_files_module_entity ON files (module, entity_type, entity_id);

            ALTER TABLE expense_invoices
                ADD COLUMN IF NOT EXISTS file_id BIGINT NULL;
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
