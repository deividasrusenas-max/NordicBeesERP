using Microsoft.EntityFrameworkCore.Migrations;

namespace NordicBeesERP.Migrations;

public partial class TestMigrationPattern : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE order_lines
                ADD COLUMN IF NOT EXISTS lot_number VARCHAR(100) NULL;
        ");
    }
}
