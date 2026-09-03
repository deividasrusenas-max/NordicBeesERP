using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryCostDeductions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "barrel_cost_deduction",
                table: "deliveries",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "other_cost_deduction",
                table: "deliveries",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "transport_cost_deduction",
                table: "deliveries",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "barrel_cost_deduction",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "other_cost_deduction",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "transport_cost_deduction",
                table: "deliveries");
        }
    }
}
