using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 31, 13, 34, 20, 878, DateTimeKind.Local).AddTicks(4960), new DateTime(2026, 5, 31, 13, 34, 20, 878, DateTimeKind.Local).AddTicks(5000) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 31, 13, 34, 20, 878, DateTimeKind.Local).AddTicks(5000), new DateTime(2026, 5, 31, 13, 34, 20, 878, DateTimeKind.Local).AddTicks(5000) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 31, 13, 34, 20, 878, DateTimeKind.Local).AddTicks(5000), new DateTime(2026, 5, 31, 13, 34, 20, 878, DateTimeKind.Local).AddTicks(5000) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 31, 13, 34, 20, 878, DateTimeKind.Local).AddTicks(5000), new DateTime(2026, 5, 31, 13, 34, 20, 878, DateTimeKind.Local).AddTicks(5000) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 31, 13, 34, 20, 878, DateTimeKind.Local).AddTicks(5010), new DateTime(2026, 5, 31, 13, 34, 20, 878, DateTimeKind.Local).AddTicks(5010) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 17, 9, 22, 642, DateTimeKind.Local).AddTicks(8810), new DateTime(2026, 5, 28, 17, 9, 22, 642, DateTimeKind.Local).AddTicks(8860) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 17, 9, 22, 642, DateTimeKind.Local).AddTicks(8870), new DateTime(2026, 5, 28, 17, 9, 22, 642, DateTimeKind.Local).AddTicks(8880) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 17, 9, 22, 642, DateTimeKind.Local).AddTicks(8880), new DateTime(2026, 5, 28, 17, 9, 22, 642, DateTimeKind.Local).AddTicks(8880) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 17, 9, 22, 642, DateTimeKind.Local).AddTicks(8880), new DateTime(2026, 5, 28, 17, 9, 22, 642, DateTimeKind.Local).AddTicks(8880) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 17, 9, 22, 642, DateTimeKind.Local).AddTicks(8880), new DateTime(2026, 5, 28, 17, 9, 22, 642, DateTimeKind.Local).AddTicks(8880) });
        }
    }
}
