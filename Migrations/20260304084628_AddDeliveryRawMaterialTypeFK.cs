using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryRawMaterialTypeFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "raw_material_type_id",
                table: "deliveries",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 4, 10, 46, 27, 802, DateTimeKind.Local).AddTicks(1390), new DateTime(2026, 3, 4, 10, 46, 27, 802, DateTimeKind.Local).AddTicks(1420) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 4, 10, 46, 27, 802, DateTimeKind.Local).AddTicks(1420), new DateTime(2026, 3, 4, 10, 46, 27, 802, DateTimeKind.Local).AddTicks(1430) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 4, 10, 46, 27, 802, DateTimeKind.Local).AddTicks(1430), new DateTime(2026, 3, 4, 10, 46, 27, 802, DateTimeKind.Local).AddTicks(1430) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 4, 10, 46, 27, 802, DateTimeKind.Local).AddTicks(1430), new DateTime(2026, 3, 4, 10, 46, 27, 802, DateTimeKind.Local).AddTicks(1430) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 4, 10, 46, 27, 802, DateTimeKind.Local).AddTicks(1430), new DateTime(2026, 3, 4, 10, 46, 27, 802, DateTimeKind.Local).AddTicks(1430) });

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_raw_material_type_id",
                table: "deliveries",
                column: "raw_material_type_id");

            migrationBuilder.AddForeignKey(
                name: "FK_deliveries_raw_material_types_raw_material_type_id",
                table: "deliveries",
                column: "raw_material_type_id",
                principalTable: "raw_material_types",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_deliveries_raw_material_types_raw_material_type_id",
                table: "deliveries");

            migrationBuilder.DropIndex(
                name: "IX_deliveries_raw_material_type_id",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "raw_material_type_id",
                table: "deliveries");

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 4, 10, 44, 9, 9, DateTimeKind.Local).AddTicks(1400), new DateTime(2026, 3, 4, 10, 44, 9, 9, DateTimeKind.Local).AddTicks(1470) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 4, 10, 44, 9, 9, DateTimeKind.Local).AddTicks(1470), new DateTime(2026, 3, 4, 10, 44, 9, 9, DateTimeKind.Local).AddTicks(1470) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 4, 10, 44, 9, 9, DateTimeKind.Local).AddTicks(1480), new DateTime(2026, 3, 4, 10, 44, 9, 9, DateTimeKind.Local).AddTicks(1480) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 4, 10, 44, 9, 9, DateTimeKind.Local).AddTicks(1480), new DateTime(2026, 3, 4, 10, 44, 9, 9, DateTimeKind.Local).AddTicks(1480) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 3, 4, 10, 44, 9, 9, DateTimeKind.Local).AddTicks(1480), new DateTime(2026, 3, 4, 10, 44, 9, 9, DateTimeKind.Local).AddTicks(1480) });
        }
    }
}
