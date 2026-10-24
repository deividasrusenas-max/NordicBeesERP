using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// OFFSET-01: settlement_offsets / settlement_offset_lines plus the 'offset' enum values on payments and expense_payments.
    /// Hand-written (no [Migration] attribute / Designer, like AddInvoiceSupplyDate): mirrors
    /// Migrations/Scripts/20261024_settlement_offsets.sql, which the human applies (it also inserts the __EFMigrationsHistory row).
    /// </summary>
    public partial class AddSettlementOffsets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS settlement_offsets (
  id INT NOT NULL AUTO_INCREMENT, offset_number VARCHAR(30) NOT NULL, offset_date DATE NOT NULL, partner_id INT NOT NULL,
  currency VARCHAR(3) NOT NULL DEFAULT 'EUR', amount DECIMAL(12,2) NOT NULL, status VARCHAR(20) NOT NULL DEFAULT 'confirmed',
  notes TEXT NULL, created_by INT NULL, created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, confirmed_by INT NULL,
  confirmed_at DATETIME NULL, cancelled_by INT NULL, cancelled_at DATETIME NULL, cancel_reason TEXT NULL,
  PRIMARY KEY (id), UNIQUE KEY uq_settlement_offsets_number (offset_number), KEY ix_settlement_offsets_partner (partner_id),
  CONSTRAINT fk_settlement_offsets_partner FOREIGN KEY (partner_id) REFERENCES business_partners (id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS settlement_offset_lines (
  id INT NOT NULL AUTO_INCREMENT, offset_id INT NOT NULL, side VARCHAR(20) NOT NULL, invoice_id INT NULL, expense_invoice_id INT NULL,
  amount DECIMAL(12,2) NOT NULL, open_before DECIMAL(12,2) NOT NULL, payment_id INT NULL, expense_payment_id INT NULL,
  PRIMARY KEY (id), KEY ix_settlement_offset_lines_offset (offset_id),
  CONSTRAINT fk_settlement_offset_lines_offset FOREIGN KEY (offset_id) REFERENCES settlement_offsets (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");
            migrationBuilder.Sql("ALTER TABLE payments MODIFY payment_method ENUM('bank_transfer','cash','card','other','offset') COLLATE utf8mb4_unicode_ci DEFAULT 'bank_transfer', MODIFY source ENUM('manual','bank_import','offset') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'manual';");
            migrationBuilder.Sql("ALTER TABLE expense_payments MODIFY payment_method ENUM('BANK','CASH','OTHER','OFFSET') COLLATE utf8mb4_unicode_ci DEFAULT 'BANK';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS settlement_offset_lines;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS settlement_offsets;");
            migrationBuilder.Sql("ALTER TABLE payments MODIFY payment_method ENUM('bank_transfer','cash','card','other') COLLATE utf8mb4_unicode_ci DEFAULT 'bank_transfer', MODIFY source ENUM('manual','bank_import') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'manual';");
            migrationBuilder.Sql("ALTER TABLE expense_payments MODIFY payment_method ENUM('BANK','CASH','OTHER') COLLATE utf8mb4_unicode_ci DEFAULT 'BANK';");
        }
    }
}
