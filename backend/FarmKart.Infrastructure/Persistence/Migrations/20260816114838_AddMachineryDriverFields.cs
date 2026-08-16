using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmKart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineryDriverFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MachineryRental_Amounts_NonNegative",
                table: "MachineryRentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Machinery_DailyRent_NonNegative",
                table: "Machinery");

            migrationBuilder.AddColumn<decimal>(
                name: "DriverAmount",
                table: "MachineryRentals",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DriverChargePerDaySnapshot",
                table: "MachineryRentals",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "DriverRequired",
                table: "MachineryRentals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MachineryAmount",
                table: "MachineryRentals",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "MachineryRentals",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "DriverAvailable",
                table: "Machinery",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DriverChargePerDay",
                table: "Machinery",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DriverName",
                table: "Machinery",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverNotes",
                table: "Machinery",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverPhone",
                table: "Machinery",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MachineryRental_Amounts_NonNegative",
                table: "MachineryRentals",
                sql: "[TotalRentAmount] >= 0 AND [TotalPayableAmount] >= 0 AND [SecurityDepositSnapshot] >= 0 AND [RentPerDaySnapshot] >= 0 AND [DriverChargePerDaySnapshot] >= 0 AND [MachineryAmount] >= 0 AND [DriverAmount] >= 0 AND [TotalAmount] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Machinery_DriverAvailable",
                table: "Machinery",
                column: "DriverAvailable");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Machinery_DailyRent_NonNegative",
                table: "Machinery",
                sql: "[DailyRent] >= 0 AND [SecurityDeposit] >= 0 AND [DriverChargePerDay] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MachineryRental_Amounts_NonNegative",
                table: "MachineryRentals");

            migrationBuilder.DropIndex(
                name: "IX_Machinery_DriverAvailable",
                table: "Machinery");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Machinery_DailyRent_NonNegative",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "DriverAmount",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "DriverChargePerDaySnapshot",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "DriverRequired",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "MachineryAmount",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "DriverAvailable",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "DriverChargePerDay",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "DriverName",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "DriverNotes",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "DriverPhone",
                table: "Machinery");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MachineryRental_Amounts_NonNegative",
                table: "MachineryRentals",
                sql: "[TotalRentAmount] >= 0 AND [TotalPayableAmount] >= 0 AND [SecurityDepositSnapshot] >= 0 AND [RentPerDaySnapshot] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Machinery_DailyRent_NonNegative",
                table: "Machinery",
                sql: "[DailyRent] >= 0 AND [SecurityDeposit] >= 0");
        }
    }
}
