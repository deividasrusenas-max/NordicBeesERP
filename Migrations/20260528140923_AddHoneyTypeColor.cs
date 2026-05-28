using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class AddHoneyTypeColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "honey_types",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "color",
                table: "honey_types");

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
    }
}
