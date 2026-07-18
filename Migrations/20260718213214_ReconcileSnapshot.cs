using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 7, 18, 21, 32, 14, 376, DateTimeKind.Utc).AddTicks(3500));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 7, 18, 21, 32, 14, 376, DateTimeKind.Utc).AddTicks(3510));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 3,
                column: "created_at",
                value: new DateTime(2026, 7, 18, 21, 32, 14, 376, DateTimeKind.Utc).AddTicks(3510));

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 19, 0, 32, 14, 375, DateTimeKind.Local).AddTicks(9950), new DateTime(2026, 7, 19, 0, 32, 14, 375, DateTimeKind.Local).AddTicks(9990) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 19, 0, 32, 14, 375, DateTimeKind.Local).AddTicks(9990), new DateTime(2026, 7, 19, 0, 32, 14, 376, DateTimeKind.Local) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 19, 0, 32, 14, 376, DateTimeKind.Local), new DateTime(2026, 7, 19, 0, 32, 14, 376, DateTimeKind.Local) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 19, 0, 32, 14, 376, DateTimeKind.Local), new DateTime(2026, 7, 19, 0, 32, 14, 376, DateTimeKind.Local) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 19, 0, 32, 14, 376, DateTimeKind.Local), new DateTime(2026, 7, 19, 0, 32, 14, 376, DateTimeKind.Local) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 7, 18, 21, 29, 27, 389, DateTimeKind.Utc).AddTicks(3040));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 7, 18, 21, 29, 27, 389, DateTimeKind.Utc).AddTicks(3040));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 3,
                column: "created_at",
                value: new DateTime(2026, 7, 18, 21, 29, 27, 389, DateTimeKind.Utc).AddTicks(3040));

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 19, 0, 29, 27, 388, DateTimeKind.Local).AddTicks(9640), new DateTime(2026, 7, 19, 0, 29, 27, 388, DateTimeKind.Local).AddTicks(9670) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 19, 0, 29, 27, 388, DateTimeKind.Local).AddTicks(9670), new DateTime(2026, 7, 19, 0, 29, 27, 388, DateTimeKind.Local).AddTicks(9680) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 19, 0, 29, 27, 388, DateTimeKind.Local).AddTicks(9680), new DateTime(2026, 7, 19, 0, 29, 27, 388, DateTimeKind.Local).AddTicks(9680) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 19, 0, 29, 27, 388, DateTimeKind.Local).AddTicks(9680), new DateTime(2026, 7, 19, 0, 29, 27, 388, DateTimeKind.Local).AddTicks(9680) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 19, 0, 29, 27, 388, DateTimeKind.Local).AddTicks(9680), new DateTime(2026, 7, 19, 0, 29, 27, 388, DateTimeKind.Local).AddTicks(9680) });
        }
    }
}
