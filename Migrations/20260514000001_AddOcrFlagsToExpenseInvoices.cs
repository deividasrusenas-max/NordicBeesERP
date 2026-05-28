using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// OcrFlags column already exists in expense_invoices table (added manually).
    /// This migration is a no-op to keep EF Core migration tracking in sync.
    /// </summary>
    public partial class AddOcrFlagsToExpenseInvoices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SELECT 1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}