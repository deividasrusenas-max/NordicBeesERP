using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// Drops the legacy warehouse_stocks table together with its only users (Inventory, NewBatch,
    /// BatchSelectDialog, ProductionService). Hand-edited: EF also scaffolded UpdateData calls that would
    /// overwrite seeded created_at values (HasData uses DateTime.Now) -- removed on purpose.
    /// </summary>
    public partial class DropWarehouseStocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Empty, unreferenced legacy table (0 rows on prod/staging, verified 2026-10-14).
            // Real stock lives in containers + stock_movements. IF EXISTS: valid on MySQL 8.0 and MariaDB 11.8.
            migrationBuilder.Sql("DROP TABLE IF EXISTS `warehouse_stocks`;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "warehouse_stocks",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    available_quantity = table.Column<decimal>(type: "decimal(65,30)", nullable: false, computedColumnSql: "(quantity - reserved_quantity)", stored: true),
                    expiration_date = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    honey_batch_id = table.Column<int>(type: "int", nullable: false),
                    last_movement_date = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    lot_number = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    product_id = table.Column<int>(type: "int", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    reserved_quantity = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    warehouse_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_stocks", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_stocks_lot_number",
                table: "warehouse_stocks",
                column: "lot_number");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_stocks_product_id",
                table: "warehouse_stocks",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_stocks_warehouse_id",
                table: "warehouse_stocks",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_stocks_warehouse_id_product_id_lot_number",
                table: "warehouse_stocks",
                columns: new[] { "warehouse_id", "product_id", "lot_number" },
                unique: true);
        }
    }
}
