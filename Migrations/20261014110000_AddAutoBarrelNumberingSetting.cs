using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// Seeds app_settings 'AutoBarrelNumbering' = false (operator enters a start ID again; true switches back to the
    /// automatic TP codes). Idempotent plain INSERT ... WHERE NOT EXISTS, valid on MySQL 8.0 and MariaDB 11.8.
    /// A missing row is treated as false by the code, so this only makes the setting visible.
    /// </summary>
    public partial class AddAutoBarrelNumberingSetting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "INSERT INTO app_settings (setting_key, setting_value) " +
                "SELECT 'AutoBarrelNumbering', 'false' FROM DUAL " +
                "WHERE NOT EXISTS (SELECT 1 FROM app_settings WHERE setting_key = 'AutoBarrelNumbering');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM app_settings WHERE setting_key = 'AutoBarrelNumbering';");
        }
    }
}
