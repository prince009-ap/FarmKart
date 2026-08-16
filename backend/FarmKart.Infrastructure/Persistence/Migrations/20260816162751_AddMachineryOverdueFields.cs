using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmKart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineryOverdueFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OverdueCharge",
                table: "MachineryRentals",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OverdueDays",
                table: "MachineryRentals",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverdueCharge",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "OverdueDays",
                table: "MachineryRentals");
        }
    }
}
