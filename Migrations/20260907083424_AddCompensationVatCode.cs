using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCompensationVatCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "compensation_vat_code",
                table: "business_partners",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 9, 7, 8, 34, 23, 838, DateTimeKind.Utc).AddTicks(4910));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 9, 7, 8, 34, 23, 838, DateTimeKind.Utc).AddTicks(4910));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 3,
                column: "created_at",
                value: new DateTime(2026, 9, 7, 8, 34, 23, 838, DateTimeKind.Utc).AddTicks(4910));

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 7, 11, 34, 23, 838, DateTimeKind.Local).AddTicks(1400), new DateTime(2026, 9, 7, 11, 34, 23, 838, DateTimeKind.Local).AddTicks(1430) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 7, 11, 34, 23, 838, DateTimeKind.Local).AddTicks(1430), new DateTime(2026, 9, 7, 11, 34, 23, 838, DateTimeKind.Local).AddTicks(1430) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 7, 11, 34, 23, 838, DateTimeKind.Local).AddTicks(1430), new DateTime(2026, 9, 7, 11, 34, 23, 838, DateTimeKind.Local).AddTicks(1430) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 7, 11, 34, 23, 838, DateTimeKind.Local).AddTicks(1440), new DateTime(2026, 9, 7, 11, 34, 23, 838, DateTimeKind.Local).AddTicks(1440) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 7, 11, 34, 23, 838, DateTimeKind.Local).AddTicks(1440), new DateTime(2026, 9, 7, 11, 34, 23, 838, DateTimeKind.Local).AddTicks(1440) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "compensation_vat_code",
                table: "business_partners");

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 9, 6, 18, 33, 11, 933, DateTimeKind.Utc).AddTicks(1250));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 9, 6, 18, 33, 11, 933, DateTimeKind.Utc).AddTicks(1250));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 3,
                column: "created_at",
                value: new DateTime(2026, 9, 6, 18, 33, 11, 933, DateTimeKind.Utc).AddTicks(1250));

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 6, 21, 33, 11, 932, DateTimeKind.Local).AddTicks(5950), new DateTime(2026, 9, 6, 21, 33, 11, 932, DateTimeKind.Local).AddTicks(6020) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 6, 21, 33, 11, 932, DateTimeKind.Local).AddTicks(6030), new DateTime(2026, 9, 6, 21, 33, 11, 932, DateTimeKind.Local).AddTicks(6030) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 6, 21, 33, 11, 932, DateTimeKind.Local).AddTicks(6030), new DateTime(2026, 9, 6, 21, 33, 11, 932, DateTimeKind.Local).AddTicks(6030) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 6, 21, 33, 11, 932, DateTimeKind.Local).AddTicks(6030), new DateTime(2026, 9, 6, 21, 33, 11, 932, DateTimeKind.Local).AddTicks(6030) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 9, 6, 21, 33, 11, 932, DateTimeKind.Local).AddTicks(6030), new DateTime(2026, 9, 6, 21, 33, 11, 932, DateTimeKind.Local).AddTicks(6040) });
        }
    }
}
