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
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS app_settings (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    setting_key VARCHAR(100) NOT NULL UNIQUE,
                    setting_value TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                );
            ");
            migrationBuilder.Sql("ALTER TABLE business_partners ADD COLUMN IF NOT EXISTS default_expense_category_id INT NULL DEFAULT NULL;");
            migrationBuilder.Sql("ALTER TABLE business_partners MODIFY COLUMN partner_type ENUM('customer','supplier','both','expense_supplier') NOT NULL DEFAULT 'customer';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE business_partners MODIFY COLUMN partner_type ENUM('customer','supplier','both') NOT NULL DEFAULT 'customer';");
            migrationBuilder.Sql("ALTER TABLE business_partners DROP COLUMN IF EXISTS default_expense_category_id;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS app_settings;");
        }
    }
}