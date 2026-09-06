using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerRoleFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_customer",
                table: "business_partners",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_expense_supplier",
                table: "business_partners",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_individual",
                table: "business_partners",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_supplier",
                table: "business_partners",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_customer",
                table: "business_partners");

            migrationBuilder.DropColumn(
                name: "is_expense_supplier",
                table: "business_partners");

            migrationBuilder.DropColumn(
                name: "is_individual",
                table: "business_partners");

            migrationBuilder.DropColumn(
                name: "is_supplier",
                table: "business_partners");
        }
    }
}
