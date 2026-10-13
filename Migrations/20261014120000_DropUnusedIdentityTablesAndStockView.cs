using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// Tech-debt cleanup. Drops leftovers no code references:
    ///  - 7 empty ASP.NET Identity tables (auth uses erp_users + BlazorAuthStateProvider; Identity is not wired).
    ///    Verified 2026-10-14 on prod + staging (lakstena-dev, MariaDB 11.8): all 0 rows, no FKs from other tables.
    ///  - the unused `warehouse_stock` VIEW (only present on dev/test; EF maps WarehouseStock -> `warehouse_stocks`).
    /// Child tables are dropped before AspNetUsers/AspNetRoles. IF EXISTS is valid on MySQL 8.0 and MariaDB 11.8.
    /// `invoices.PaymentTermId1` and `users` were already gone on prod, staging and dev, so they are not touched.
    /// </summary>
    public partial class DropUnusedIdentityTablesAndStockView : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetUserTokens`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetUserRoles`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetUserLogins`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetUserClaims`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetRoleClaims`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetUsers`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetRoles`;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS `warehouse_stock`;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: the dropped objects were empty and unused. Restore from the pre-migration dump if ever needed.
        }
    }
}
