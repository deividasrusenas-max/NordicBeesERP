using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// Bucket tare auto-calculation: <c>bucket_tare_ranges</c> (seeded 1–16 → 0.40, 16.01–40 → 0.80, 40.01–55 → 1.10)
    /// and <c>containers.tare_manual</c>. Per-container tare already lives in <c>containers.tare_weight</c>, so no new
    /// tare column. Scaffolded with <c>dotnet ef migrations add</c> and trimmed of the HasData timestamp churn.
    /// No IF [NOT] EXISTS: valid on MySQL 8.0 and MariaDB 11.8. Existing rows are not recalculated.
    /// </summary>
    public partial class AddBucketTareRanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "tare_manual",
                table: "containers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "bucket_tare_ranges",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    min_brutto_kg = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    max_brutto_kg = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    tare_kg = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bucket_tare_ranges", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // ON UPDATE is not expressible through the EF column API.
            migrationBuilder.Sql(
                "ALTER TABLE bucket_tare_ranges MODIFY updated_at DATETIME(6) NOT NULL " +
                "DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6);");

            // Seed (hardcoded literals only); skipped when the table already has rows.
            migrationBuilder.Sql(
                "INSERT INTO bucket_tare_ranges (min_brutto_kg, max_brutto_kg, tare_kg, sort_order, is_active) " +
                "SELECT s.min_brutto_kg, s.max_brutto_kg, s.tare_kg, s.sort_order, 1 FROM (" +
                "SELECT 1.00 AS min_brutto_kg, 16.00 AS max_brutto_kg, 0.40 AS tare_kg, 1 AS sort_order " +
                "UNION ALL SELECT 16.01, 40.00, 0.80, 2 " +
                "UNION ALL SELECT 40.01, 55.00, 1.10, 3) s " +
                "WHERE NOT EXISTS (SELECT 1 FROM bucket_tare_ranges);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bucket_tare_ranges");

            migrationBuilder.DropColumn(
                name: "tare_manual",
                table: "containers");
        }
    }
}
