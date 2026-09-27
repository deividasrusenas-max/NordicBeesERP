using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// Etapas 2 S4 (PLAN-ETAPAS2 §1.4, D-044 Q2/Q5/Q11): supplier_bank_accounts — a partner's known IBANs, no foreign keys.
    /// Scaffolded with <c>dotnet ef migrations add</c> and trimmed by hand: the scaffold also contained UpdateData churn on the
    /// seeded artwork_brands / raw_material_types timestamps (the model snapshot keeps its previous values). Only the new table remains.
    /// </summary>
    public partial class AddSupplierBankAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "supplier_bank_accounts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    partner_id = table.Column<int>(type: "int", nullable: false),
                    iban = table.Column<string>(type: "varchar(34)", maxLength: 34, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_invoice_id = table.Column<int>(type: "int", nullable: true),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_bank_accounts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "idx_iban",
                table: "supplier_bank_accounts",
                column: "iban");

            migrationBuilder.CreateIndex(
                name: "uq_partner_iban",
                table: "supplier_bank_accounts",
                columns: new[] { "partner_id", "iban" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_bank_accounts");
        }
    }
}
