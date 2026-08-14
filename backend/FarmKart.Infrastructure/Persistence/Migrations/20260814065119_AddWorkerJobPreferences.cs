using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmKart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerJobPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccommodationPreference",
                table: "WorkerProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FoodPreference",
                table: "WorkerProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumDailyWage",
                table: "WorkerProfiles",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLocations",
                table: "WorkerProfiles",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredWorkCategories",
                table: "WorkerProfiles",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredWorkingHours",
                table: "WorkerProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkerProfile_MinimumDailyWage_NonNegative",
                table: "WorkerProfiles",
                sql: "[MinimumDailyWage] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkerProfile_MinimumDailyWage_NonNegative",
                table: "WorkerProfiles");

            migrationBuilder.DropColumn(
                name: "AccommodationPreference",
                table: "WorkerProfiles");

            migrationBuilder.DropColumn(
                name: "FoodPreference",
                table: "WorkerProfiles");

            migrationBuilder.DropColumn(
                name: "MinimumDailyWage",
                table: "WorkerProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredLocations",
                table: "WorkerProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredWorkCategories",
                table: "WorkerProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredWorkingHours",
                table: "WorkerProfiles");
        }
    }
}
