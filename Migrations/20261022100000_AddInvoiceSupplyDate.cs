using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// invoices.supply_date: the supply (delivery) date of a ULAK invoice created from a delivery, now that invoice_date is
    /// the issue day. NULL = same as invoice_date. Hand-written (no [Migration] attribute / Designer, like InvoiceStatusAcceptsCancelled):
    /// mirrors Migrations/Scripts/20261022_invoice_supply_date.sql, which the human applies; the script also inserts the
    /// __EFMigrationsHistory row.
    /// </summary>
    public partial class AddInvoiceSupplyDate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE invoices ADD COLUMN supply_date DATE NULL AFTER invoice_date;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE invoices DROP COLUMN supply_date;");
        }
    }
}
