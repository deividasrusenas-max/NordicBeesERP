using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class AddContainerCodeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `containers` DROP INDEX `idx_container_code`;");
            migrationBuilder.Sql("ALTER TABLE `containers` ADD UNIQUE INDEX `idx_container_code` (`container_code`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `containers` DROP INDEX `idx_container_code`;");
            migrationBuilder.Sql("ALTER TABLE `containers` ADD INDEX `idx_container_code` (`container_code`);");
        }
    }
}
