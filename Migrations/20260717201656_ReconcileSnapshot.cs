using System;
using Microsoft.EntityFrameworkCore.Metadata;
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
            migrationBuilder.DropTable(
                name: "units");

            migrationBuilder.DropColumn(
                name: "expiration_date",
                table: "warehouse_stocks");

            migrationBuilder.DropColumn(
                name: "honey_batch_id",
                table: "warehouse_stocks");

            migrationBuilder.AddColumn<DateTime>(
                name: "inspection_at",
                table: "deliveries",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inspection_notes",
                table: "deliveries",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "inspection_result",
                table: "deliveries",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "origin_country",
                table: "deliveries",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "receipt_pdf_path",
                table: "deliveries",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "receiver_name",
                table: "deliveries",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "signed_by_type",
                table: "deliveries",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "supplier_signature_svg",
                table: "deliveries",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "supplier_signed_at",
                table: "deliveries",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_signer_name",
                table: "deliveries",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "default_vat_rate",
                table: "company_settings",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");

            migrationBuilder.AddColumn<bool>(
                name: "no_email",
                table: "business_partners",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "label_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    template_type = table.Column<int>(type: "int", nullable: false),
                    content = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    default_printer_id = table.Column<int>(type: "int", nullable: true),
                    width_mm = table.Column<decimal>(type: "decimal(5,1)", nullable: false),
                    height_mm = table.Column<decimal>(type: "decimal(5,1)", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_label_templates", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "non_conformances",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    delivery_id = table.Column<int>(type: "int", nullable: false),
                    container_id = table.Column<int>(type: "int", nullable: true),
                    description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    nc_type = table.Column<int>(type: "int", nullable: false),
                    discovered_by = table.Column<int>(type: "int", nullable: false),
                    discovered_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    corrective_action = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    closed_by = table.Column<int>(type: "int", nullable: true),
                    closed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_non_conformances", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "print_jobs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    printer_id = table.Column<int>(type: "int", nullable: false),
                    station_id = table.Column<int>(type: "int", nullable: true),
                    container_id = table.Column<int>(type: "int", nullable: false),
                    job_type = table.Column<int>(type: "int", nullable: false),
                    zpl_content = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<int>(type: "int", nullable: false),
                    retry_count = table.Column<int>(type: "int", nullable: false),
                    max_retries = table.Column<int>(type: "int", nullable: false),
                    last_error = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    done_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_print_jobs", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "units_of_measure",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name_en = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    unit_type = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units_of_measure", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 7, 17, 20, 16, 56, 10, DateTimeKind.Utc).AddTicks(7890));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 7, 17, 20, 16, 56, 10, DateTimeKind.Utc).AddTicks(7890));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 3,
                column: "created_at",
                value: new DateTime(2026, 7, 17, 20, 16, 56, 10, DateTimeKind.Utc).AddTicks(7890));

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 17, 23, 16, 56, 10, DateTimeKind.Local).AddTicks(4650), new DateTime(2026, 7, 17, 23, 16, 56, 10, DateTimeKind.Local).AddTicks(4690) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 17, 23, 16, 56, 10, DateTimeKind.Local).AddTicks(4700), new DateTime(2026, 7, 17, 23, 16, 56, 10, DateTimeKind.Local).AddTicks(4700) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 17, 23, 16, 56, 10, DateTimeKind.Local).AddTicks(4700), new DateTime(2026, 7, 17, 23, 16, 56, 10, DateTimeKind.Local).AddTicks(4700) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 17, 23, 16, 56, 10, DateTimeKind.Local).AddTicks(4700), new DateTime(2026, 7, 17, 23, 16, 56, 10, DateTimeKind.Local).AddTicks(4700) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 17, 23, 16, 56, 10, DateTimeKind.Local).AddTicks(4710), new DateTime(2026, 7, 17, 23, 16, 56, 10, DateTimeKind.Local).AddTicks(4710) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "label_templates");

            migrationBuilder.DropTable(
                name: "non_conformances");

            migrationBuilder.DropTable(
                name: "print_jobs");

            migrationBuilder.DropTable(
                name: "units_of_measure");

            migrationBuilder.DropColumn(
                name: "inspection_at",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "inspection_notes",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "inspection_result",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "origin_country",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "receipt_pdf_path",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "receiver_name",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "signed_by_type",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "supplier_signature_svg",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "supplier_signed_at",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "supplier_signer_name",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "no_email",
                table: "business_partners");

            migrationBuilder.AddColumn<DateTime>(
                name: "expiration_date",
                table: "warehouse_stocks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "honey_batch_id",
                table: "warehouse_stocks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "default_vat_rate",
                table: "company_settings",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 7, 2, 8, 42, 37, 778, DateTimeKind.Utc).AddTicks(3810));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 7, 2, 8, 42, 37, 778, DateTimeKind.Utc).AddTicks(3830));

            migrationBuilder.UpdateData(
                table: "artwork_brands",
                keyColumn: "id",
                keyValue: 3,
                column: "created_at",
                value: new DateTime(2026, 7, 2, 8, 42, 37, 778, DateTimeKind.Utc).AddTicks(3830));

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 2, 11, 42, 37, 777, DateTimeKind.Local).AddTicks(7540), new DateTime(2026, 7, 2, 11, 42, 37, 777, DateTimeKind.Local).AddTicks(7830) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 2, 11, 42, 37, 777, DateTimeKind.Local).AddTicks(7850), new DateTime(2026, 7, 2, 11, 42, 37, 777, DateTimeKind.Local).AddTicks(7860) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 2, 11, 42, 37, 777, DateTimeKind.Local).AddTicks(7860), new DateTime(2026, 7, 2, 11, 42, 37, 777, DateTimeKind.Local).AddTicks(7860) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 2, 11, 42, 37, 777, DateTimeKind.Local).AddTicks(7860), new DateTime(2026, 7, 2, 11, 42, 37, 777, DateTimeKind.Local).AddTicks(7870) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 2, 11, 42, 37, 777, DateTimeKind.Local).AddTicks(7870), new DateTime(2026, 7, 2, 11, 42, 37, 777, DateTimeKind.Local).AddTicks(7870) });
        }
    }
}
