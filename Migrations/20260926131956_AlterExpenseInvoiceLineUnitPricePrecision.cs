using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// D-039: expense_invoice_lines.unit_price decimal(12,2) → decimal(18,6) (fuel prices have 3
    /// decimals, material prices 4). Widening only — no value changes.
    /// <para>
    /// Scaffolded with <c>dotnet ef migrations add</c> and trimmed by hand: the scaffold also contained
    /// (1) <c>AddColumn file_id</c> on expense_invoices — that column already exists via the hand-written
    /// 20260915120000_AddFilesTableAndExpenseInvoiceFileId (no Designer, so the snapshot never knew it;
    /// the snapshot now does), and (2) UpdateData churn on seeded artwork_brands / raw_material_types
    /// timestamps. Both were removed; only the unit_price change remains.
    /// </para>
    /// </summary>
    public partial class AlterExpenseInvoiceLineUnitPricePrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "expense_invoice_lines",
                type: "decimal(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "expense_invoice_lines",
                type: "decimal(12,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldNullable: true);
        }
    }
}
