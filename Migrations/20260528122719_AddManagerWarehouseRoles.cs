using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerWarehouseRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "erp_users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 15, 27, 18, 780, DateTimeKind.Local).AddTicks(1540), new DateTime(2026, 5, 28, 15, 27, 18, 780, DateTimeKind.Local).AddTicks(1590) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 15, 27, 18, 780, DateTimeKind.Local).AddTicks(1600), new DateTime(2026, 5, 28, 15, 27, 18, 780, DateTimeKind.Local).AddTicks(1600) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 15, 27, 18, 780, DateTimeKind.Local).AddTicks(1600), new DateTime(2026, 5, 28, 15, 27, 18, 780, DateTimeKind.Local).AddTicks(1600) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 15, 27, 18, 780, DateTimeKind.Local).AddTicks(1600), new DateTime(2026, 5, 28, 15, 27, 18, 780, DateTimeKind.Local).AddTicks(1600) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 15, 27, 18, 780, DateTimeKind.Local).AddTicks(1600), new DateTime(2026, 5, 28, 15, 27, 18, 780, DateTimeKind.Local).AddTicks(1610) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "erp_users",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5000), new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5070) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5080), new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5080) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5100), new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5100) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5100), new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5100) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5100), new DateTime(2026, 5, 28, 0, 27, 7, 597, DateTimeKind.Local).AddTicks(5100) });
        }
    }
}
