using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// invoices.status: the C# enum InvoiceStatus has Cancelled (stored via HasConversion&lt;string&gt;) but the DB enum is
    /// ('draft','confirmed','paid','disputed'), so persisting it would fail with "Data truncated for column 'status'".
    /// Widen to the superset (existing order kept, 'cancelled' appended). The column stays NULLable with default 'draft'.
    /// Hand-written (no [Migration] attribute / Designer, like AddDunning): mirrors Migrations/Scripts/20261015_invoice_status_cancelled.sql,
    /// which the human applies; the script also inserts the __EFMigrationsHistory row. The C# enum is NOT changed.
    /// </summary>
    public partial class InvoiceStatusAcceptsCancelled : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE invoices MODIFY status ENUM('draft','confirmed','paid','disputed','cancelled') DEFAULT 'draft';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op: narrowing the enum would fail/truncate on rows holding 'cancelled'.
        }
    }
}
