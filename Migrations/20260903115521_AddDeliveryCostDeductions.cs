using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryCostDeductions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "barrel_cost_deduction",
                table: "deliveries",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "other_cost_deduction",
                table: "deliveries",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "transport_cost_deduction",
                table: "deliveries",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 9, 3, 11, 55, 21, 3, DateTimeKind.Utc).AddTicks(2520));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 9, 3, 11, 55, 21, 3, DateTimeKind.Utc).AddTicks(2520));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 3,
                column: "created_at",
                value: new DateTime(2026, 9, 3, 11, 55, 21, 3, DateTimeKind.Utc).AddTicks(2520));

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 3, 14, 55, 21, 2, DateTimeKind.Local).AddTicks(8740), new DateTime(2026, 9, 3, 14, 55, 21, 2, DateTimeKind.Local).AddTicks(8830) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 3, 14, 55, 21, 2, DateTimeKind.Local).AddTicks(8840), new DateTime(2026, 9, 3, 14, 55, 21, 2, DateTimeKind.Local).AddTicks(8840) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 3, 14, 55, 21, 2, DateTimeKind.Local).AddTicks(8840), new DateTime(2026, 9, 3, 14, 55, 21, 2, DateTimeKind.Local).AddTicks(8840) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 3, 14, 55, 21, 2, DateTimeKind.Local).AddTicks(8840), new DateTime(2026, 9, 3, 14, 55, 21, 2, DateTimeKind.Local).AddTicks(8840) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 3, 14, 55, 21, 2, DateTimeKind.Local).AddTicks(8840), new DateTime(2026, 9, 3, 14, 55, 21, 2, DateTimeKind.Local).AddTicks(8840) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "barrel_cost_deduction",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "other_cost_deduction",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "transport_cost_deduction",
                table: "deliveries");

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 8, 28, 7, 9, 29, 500, DateTimeKind.Utc).AddTicks(7140));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 8, 28, 7, 9, 29, 500, DateTimeKind.Utc).AddTicks(7140));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 3,
                column: "created_at",
                value: new DateTime(2026, 8, 28, 7, 9, 29, 500, DateTimeKind.Utc).AddTicks(7140));

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 8, 28, 10, 9, 29, 500, DateTimeKind.Local).AddTicks(3580), new DateTime(2026, 8, 28, 10, 9, 29, 500, DateTimeKind.Local).AddTicks(3650) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 8, 28, 10, 9, 29, 500, DateTimeKind.Local).AddTicks(3650), new DateTime(2026, 8, 28, 10, 9, 29, 500, DateTimeKind.Local).AddTicks(3650) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 8, 28, 10, 9, 29, 500, DateTimeKind.Local).AddTicks(3650), new DateTime(2026, 8, 28, 10, 9, 29, 500, DateTimeKind.Local).AddTicks(3650) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 8, 28, 10, 9, 29, 500, DateTimeKind.Local).AddTicks(3660), new DateTime(2026, 8, 28, 10, 9, 29, 500, DateTimeKind.Local).AddTicks(3660) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 8, 28, 10, 9, 29, 500, DateTimeKind.Local).AddTicks(3660), new DateTime(2026, 8, 28, 10, 9, 29, 500, DateTimeKind.Local).AddTicks(3660) });
        }
    }
}
