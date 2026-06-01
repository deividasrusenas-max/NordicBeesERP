using Microsoft.EntityFrameworkCore.Migrations;

namespace NordicBeesERP.Migrations
{
    public partial class FixProdMissingColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE honey_types ADD COLUMN IF NOT EXISTS color VARCHAR(7) NULL DEFAULT NULL;");
            migrationBuilder.Sql("ALTER TABLE invoices MODIFY COLUMN status ENUM('draft','confirmed','paid','disputed','cancelled') NULL DEFAULT 'draft';");
            migrationBuilder.Sql("ALTER TABLE invoices ADD COLUMN IF NOT EXISTS paid_amount DECIMAL(15,2) NOT NULL DEFAULT 0.00;");
            migrationBuilder.Sql("ALTER TABLE invoices ADD COLUMN IF NOT EXISTS payment_status ENUM('unpaid','partial','paid','overdue') NULL DEFAULT 'unpaid';");
            migrationBuilder.Sql("ALTER TABLE invoices ADD COLUMN IF NOT EXISTS last_payment_date DATE NULL DEFAULT NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE honey_types DROP COLUMN IF EXISTS color;");
        }
    }
}