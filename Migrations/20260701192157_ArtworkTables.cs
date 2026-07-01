using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class ArtworkTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artwork_assets",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    brand_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    asset_type = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    predecessor_asset_id = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artwork_assets", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "artwork_audit_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    entity_type = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    entity_id = table.Column<int>(type: "int", nullable: false),
                    action = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    details = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artwork_audit_log", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "artwork_brands",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    slug = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artwork_brands", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "artwork_comments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    version_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    body = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artwork_comments", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "artwork_version_audits",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    version_id = table.Column<int>(type: "int", nullable: false),
                    action = table.Column<string>(type: "varchar(50)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    action_details = table.Column<string>(type: "varchar(500)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    old_status = table.Column<string>(type: "varchar(30)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    new_status = table.Column<string>(type: "varchar(30)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    performed_by = table.Column<string>(type: "varchar(100)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    performed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artwork_version_audits", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "artwork_versions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    asset_id = table.Column<int>(type: "int", nullable: false),
                    version_number = table.Column<int>(type: "int", nullable: false),
                    file_type = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_path = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    original_filename = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    file_sha256 = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    preview_path = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    thumbnail_path = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    page_count = table.Column<int>(type: "int", nullable: true),
                    change_description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    uploaded_by = table.Column<int>(type: "int", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    reviewed_by = table.Column<int>(type: "int", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    review_comment = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artwork_versions", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "invoice_audit",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    invoice_id = table.Column<int>(type: "int", nullable: false),
                    invoice_number = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    action = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    action_details = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    old_status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    new_status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    performed_by = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    performed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_audit", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "artwork_brands",
                columns: new[] { "id", "created_at", "is_active", "name", "slug" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 7, 1, 19, 21, 57, 375, DateTimeKind.Utc).AddTicks(5780), true, "Nordic Bees", "nordic-bees" },
                    { 2, new DateTime(2026, 7, 1, 19, 21, 57, 375, DateTimeKind.Utc).AddTicks(5780), true, "Honeymark", "honeymark" },
                    { 3, new DateTime(2026, 7, 1, 19, 21, 57, 375, DateTimeKind.Utc).AddTicks(5780), true, "MEDŽIO", "medzio" }
                });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 1, 22, 21, 57, 371, DateTimeKind.Local).AddTicks(1770), new DateTime(2026, 7, 1, 22, 21, 57, 371, DateTimeKind.Local).AddTicks(1830) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 1, 22, 21, 57, 371, DateTimeKind.Local).AddTicks(1830), new DateTime(2026, 7, 1, 22, 21, 57, 371, DateTimeKind.Local).AddTicks(1830) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 1, 22, 21, 57, 371, DateTimeKind.Local).AddTicks(1830), new DateTime(2026, 7, 1, 22, 21, 57, 371, DateTimeKind.Local).AddTicks(1830) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 1, 22, 21, 57, 371, DateTimeKind.Local).AddTicks(1840), new DateTime(2026, 7, 1, 22, 21, 57, 371, DateTimeKind.Local).AddTicks(1840) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 7, 1, 22, 21, 57, 371, DateTimeKind.Local).AddTicks(1840), new DateTime(2026, 7, 1, 22, 21, 57, 371, DateTimeKind.Local).AddTicks(1840) });

            migrationBuilder.CreateIndex(
                name: "IX_artwork_assets_predecessor_asset_id",
                table: "artwork_assets",
                column: "predecessor_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_artwork_assets_status",
                table: "artwork_assets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_brand_asset_name",
                table: "artwork_assets",
                columns: new[] { "brand_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_artwork_audit_log_entity_type_entity_id",
                table: "artwork_audit_log",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_artwork_brands_name",
                table: "artwork_brands",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artwork_brands_slug",
                table: "artwork_brands",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artwork_version_audits_version_id",
                table: "artwork_version_audits",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "IX_artwork_comments_version_id",
                table: "artwork_comments",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "idx_asset_status",
                table: "artwork_versions",
                columns: new[] { "asset_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_artwork_versions_file_sha256",
                table: "artwork_versions",
                column: "file_sha256");

            migrationBuilder.CreateIndex(
                name: "uq_asset_version",
                table: "artwork_versions",
                columns: new[] { "asset_id", "version_number" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artwork_assets");

            migrationBuilder.DropTable(
                name: "artwork_audit_log");

            migrationBuilder.DropTable(
                name: "artwork_brands");

            migrationBuilder.DropTable(
                name: "artwork_comments");

            migrationBuilder.DropTable(
                name: "artwork_version_audits");

            migrationBuilder.DropTable(
                name: "artwork_versions");

            migrationBuilder.DropTable(
                name: "invoice_audit");

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7170), new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7220) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7220), new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7220) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7230), new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7230) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7230), new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7230) });

            migrationBuilder.UpdateData(
                table: "raw_material_types",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "created_at", "updated_at" },
                values: new object[] { new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7230), new DateTime(2026, 6, 2, 15, 49, 58, 199, DateTimeKind.Local).AddTicks(7230) });
        }
    }
}
