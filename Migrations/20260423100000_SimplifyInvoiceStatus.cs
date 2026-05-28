using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyInvoiceStatus : Migration
    {
        // Workflow: draft → confirmed → paid → disputed
        // Old ENUM: draft, issued, paid, cancelled
        // New ENUM: draft, confirmed, paid, disputed

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET SESSION sql_mode = 'ALLOW_INVALID_DATES';");
            migrationBuilder.Sql("ALTER TABLE invoices MODIFY COLUMN status ENUM('draft','issued','paid','cancelled','confirmed','disputed') DEFAULT 'draft';");
            migrationBuilder.Sql("UPDATE invoices SET status = 'confirmed' WHERE status = 'issued';");
            migrationBuilder.Sql("UPDATE invoices SET status = 'disputed'  WHERE status = 'cancelled';");
            migrationBuilder.Sql("ALTER TABLE invoices MODIFY COLUMN status ENUM('draft','confirmed','paid','disputed') DEFAULT 'draft';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET SESSION sql_mode = 'ALLOW_INVALID_DATES';");
            migrationBuilder.Sql("ALTER TABLE invoices MODIFY COLUMN status ENUM('draft','confirmed','paid','cancelled','confirmed','disputed') DEFAULT 'draft';");
            migrationBuilder.Sql("UPDATE invoices SET status = 'issued'    WHERE status = 'confirmed';");
            migrationBuilder.Sql("UPDATE invoices SET status = 'cancelled' WHERE status = 'disputed';");
            migrationBuilder.Sql("ALTER TABLE invoices MODIFY COLUMN status ENUM('draft','issued','paid','cancelled') DEFAULT 'draft';");
        }
    }
}
