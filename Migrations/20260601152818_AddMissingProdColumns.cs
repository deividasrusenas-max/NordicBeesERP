using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingProdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS app_settings (id INT AUTO_INCREMENT PRIMARY KEY, setting_key VARCHAR(100) NOT NULL UNIQUE, setting_value TEXT, created_at DATETIME DEFAULT CURRENT_TIMESTAMP, updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP);");
            migrationBuilder.Sql("ALTER TABLE business_partners ADD COLUMN IF NOT EXISTS default_expense_category_id INT NULL DEFAULT NULL;");
            migrationBuilder.Sql("ALTER TABLE business_partners MODIFY COLUMN partner_type ENUM('customer','supplier','both','expense_supplier') NOT NULL DEFAULT 'customer';");
            migrationBuilder.Sql("ALTER TABLE expense_categories ADD COLUMN IF NOT EXISTS sort_order INT NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE honey_types ADD COLUMN IF NOT EXISTS color VARCHAR(7) NULL DEFAULT NULL;");
            migrationBuilder.Sql("ALTER TABLE invoices MODIFY COLUMN status ENUM('draft','confirmed','paid','disputed','cancelled') NULL DEFAULT 'draft';");
            migrationBuilder.Sql("ALTER TABLE invoices ADD COLUMN IF NOT EXISTS paid_amount DECIMAL(15,2) NOT NULL DEFAULT 0.00;");
            migrationBuilder.Sql("ALTER TABLE invoices ADD COLUMN IF NOT EXISTS payment_status ENUM('unpaid','partial','paid','overdue') NULL DEFAULT 'unpaid';");
            migrationBuilder.Sql("ALTER TABLE invoices ADD COLUMN IF NOT EXISTS last_payment_date DATE NULL DEFAULT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS app_settings;");
            migrationBuilder.Sql("ALTER TABLE business_partners MODIFY COLUMN partner_type ENUM('customer','supplier','both') NOT NULL DEFAULT 'customer';");
            migrationBuilder.Sql("ALTER TABLE invoices MODIFY COLUMN status ENUM('draft','issued','paid','cancelled') NULL DEFAULT 'draft';");
        }
    }
}