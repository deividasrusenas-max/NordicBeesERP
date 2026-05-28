using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseInvoiceDuplicateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_expense_invoices_supplier_invoice",
                table: "expense_invoices",
                columns: new[] { "supplier_id", "invoice_number" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_expense_invoices_supplier_invoice",
                table: "expense_invoices");
        }
    }
}
