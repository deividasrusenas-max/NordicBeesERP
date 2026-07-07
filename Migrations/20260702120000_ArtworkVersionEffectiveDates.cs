using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class ArtworkVersionEffectiveDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE artwork_versions 
                ADD COLUMN effective_from DATE NULL AFTER reviewed_at,
                ADD COLUMN effective_to DATE NULL AFTER effective_from;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE artwork_versions 
                DROP COLUMN effective_to,
                DROP COLUMN effective_from;
            ");
        }
    }
}
