using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmKart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineryRentalUserBased : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Machinery_FarmerProfiles_OwnerFarmerProfileId",
                table: "Machinery");

            migrationBuilder.DropForeignKey(
                name: "FK_Machinery_MachineryCategories_MachineryCategoryId",
                table: "Machinery");

            migrationBuilder.DropForeignKey(
                name: "FK_MachineryRentals_FarmerProfiles_OwnerFarmerProfileId",
                table: "MachineryRentals");

            migrationBuilder.DropForeignKey(
                name: "FK_MachineryRentals_FarmerProfiles_RenterFarmerProfileId",
                table: "MachineryRentals");

            migrationBuilder.DropForeignKey(
                name: "FK_MachineryRentals_MachineryRentalRequests_MachineryRentalRequestId",
                table: "MachineryRentals");

            migrationBuilder.DropTable(
                name: "MachineryCategories");

            migrationBuilder.DropTable(
                name: "MachineryRentalRequests");

            migrationBuilder.DropIndex(
                name: "IX_MachineryRentals_MachineryRentalRequestId",
                table: "MachineryRentals");

            migrationBuilder.DropIndex(
                name: "IX_MachineryRentals_OwnerFarmerProfileId",
                table: "MachineryRentals");

            migrationBuilder.DropIndex(
                name: "IX_MachineryRentals_RenterFarmerProfileId",
                table: "MachineryRentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MachineryRental_Amounts_NonNegative",
                table: "MachineryRentals");

            migrationBuilder.DropIndex(
                name: "IX_Machinery_MachineryCategoryId",
                table: "Machinery");

            migrationBuilder.DropIndex(
                name: "IX_Machinery_OwnerFarmerProfileId",
                table: "Machinery");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Machinery_RentValues_NonNegative",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "MachineryRentalRequestId",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "OwnerFarmerProfileId",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "RenterFarmerProfileId",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "MachineryCategoryId",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "MonthlyRent",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "OwnerFarmerProfileId",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "WeeklyRent",
                table: "Machinery");

            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                table: "MachineryRentals",
                newName: "TotalRentAmount");

            migrationBuilder.RenameColumn(
                name: "SecurityDeposit",
                table: "MachineryRentals",
                newName: "TotalPayableAmount");

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "MachineryRentals",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "MachineryRentals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "MachineryRentals",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "MachineryRentals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                table: "MachineryRentals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTransactionRef",
                table: "MachineryRentals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RentPerDaySnapshot",
                table: "MachineryRentals",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RentalDays",
                table: "MachineryRentals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RenterUserId",
                table: "MachineryRentals",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SecurityDepositSnapshot",
                table: "MachineryRentals",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "MachineryImages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Machinery",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Machinery",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Machinery",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Machinery",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Pincode",
                table: "Machinery",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Machinery",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentals_OwnerUserId",
                table: "MachineryRentals",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentals_RentalStatus",
                table: "MachineryRentals",
                column: "RentalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentals_RenterUserId",
                table: "MachineryRentals",
                column: "RenterUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MachineryRental_Amounts_NonNegative",
                table: "MachineryRentals",
                sql: "[TotalRentAmount] >= 0 AND [TotalPayableAmount] >= 0 AND [SecurityDepositSnapshot] >= 0 AND [RentPerDaySnapshot] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MachineryRental_RentalDays_Positive",
                table: "MachineryRentals",
                sql: "[RentalDays] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Machinery_Category",
                table: "Machinery",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Machinery_IsActive",
                table: "Machinery",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Machinery_OwnerUserId",
                table: "Machinery",
                column: "OwnerUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Machinery_DailyRent_NonNegative",
                table: "Machinery",
                sql: "[DailyRent] >= 0 AND [SecurityDeposit] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MachineryRentals_OwnerUserId",
                table: "MachineryRentals");

            migrationBuilder.DropIndex(
                name: "IX_MachineryRentals_RentalStatus",
                table: "MachineryRentals");

            migrationBuilder.DropIndex(
                name: "IX_MachineryRentals_RenterUserId",
                table: "MachineryRentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MachineryRental_Amounts_NonNegative",
                table: "MachineryRentals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MachineryRental_RentalDays_Positive",
                table: "MachineryRentals");

            migrationBuilder.DropIndex(
                name: "IX_Machinery_Category",
                table: "Machinery");

            migrationBuilder.DropIndex(
                name: "IX_Machinery_IsActive",
                table: "Machinery");

            migrationBuilder.DropIndex(
                name: "IX_Machinery_OwnerUserId",
                table: "Machinery");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Machinery_DailyRent_NonNegative",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "PaymentTransactionRef",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "RentPerDaySnapshot",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "RentalDays",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "RenterUserId",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "SecurityDepositSnapshot",
                table: "MachineryRentals");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "MachineryImages");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "Pincode",
                table: "Machinery");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Machinery");

            migrationBuilder.RenameColumn(
                name: "TotalRentAmount",
                table: "MachineryRentals",
                newName: "TotalAmount");

            migrationBuilder.RenameColumn(
                name: "TotalPayableAmount",
                table: "MachineryRentals",
                newName: "SecurityDeposit");

            migrationBuilder.AddColumn<Guid>(
                name: "MachineryRentalRequestId",
                table: "MachineryRentals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerFarmerProfileId",
                table: "MachineryRentals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RenterFarmerProfileId",
                table: "MachineryRentals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "MachineryCategoryId",
                table: "Machinery",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyRent",
                table: "Machinery",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerFarmerProfileId",
                table: "Machinery",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "WeeklyRent",
                table: "Machinery",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MachineryCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MachineryRentalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerFarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RenterFarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestStatus = table.Column<int>(type: "int", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SecurityDeposit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryRentalRequests", x => x.Id);
                    table.CheckConstraint("CK_MachineryRentalRequest_Amounts_NonNegative", "[RequestedAmount] >= 0 AND [SecurityDeposit] >= 0");
                    table.CheckConstraint("CK_MachineryRentalRequest_DateRange", "[EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_MachineryRentalRequests_FarmerProfiles_OwnerFarmerProfileId",
                        column: x => x.OwnerFarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineryRentalRequests_FarmerProfiles_RenterFarmerProfileId",
                        column: x => x.RenterFarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineryRentalRequests_Machinery_MachineryId",
                        column: x => x.MachineryId,
                        principalTable: "Machinery",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentals_MachineryRentalRequestId",
                table: "MachineryRentals",
                column: "MachineryRentalRequestId",
                unique: true,
                filter: "[MachineryRentalRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentals_OwnerFarmerProfileId",
                table: "MachineryRentals",
                column: "OwnerFarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentals_RenterFarmerProfileId",
                table: "MachineryRentals",
                column: "RenterFarmerProfileId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MachineryRental_Amounts_NonNegative",
                table: "MachineryRentals",
                sql: "[TotalAmount] >= 0 AND [SecurityDeposit] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Machinery_MachineryCategoryId",
                table: "Machinery",
                column: "MachineryCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Machinery_OwnerFarmerProfileId",
                table: "Machinery",
                column: "OwnerFarmerProfileId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Machinery_RentValues_NonNegative",
                table: "Machinery",
                sql: "[DailyRent] >= 0 AND ([WeeklyRent] IS NULL OR [WeeklyRent] >= 0) AND ([MonthlyRent] IS NULL OR [MonthlyRent] >= 0) AND [SecurityDeposit] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryCategories_Name",
                table: "MachineryCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentalRequests_MachineryId",
                table: "MachineryRentalRequests",
                column: "MachineryId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentalRequests_OwnerFarmerProfileId",
                table: "MachineryRentalRequests",
                column: "OwnerFarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentalRequests_RenterFarmerProfileId",
                table: "MachineryRentalRequests",
                column: "RenterFarmerProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Machinery_FarmerProfiles_OwnerFarmerProfileId",
                table: "Machinery",
                column: "OwnerFarmerProfileId",
                principalTable: "FarmerProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Machinery_MachineryCategories_MachineryCategoryId",
                table: "Machinery",
                column: "MachineryCategoryId",
                principalTable: "MachineryCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MachineryRentals_FarmerProfiles_OwnerFarmerProfileId",
                table: "MachineryRentals",
                column: "OwnerFarmerProfileId",
                principalTable: "FarmerProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MachineryRentals_FarmerProfiles_RenterFarmerProfileId",
                table: "MachineryRentals",
                column: "RenterFarmerProfileId",
                principalTable: "FarmerProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MachineryRentals_MachineryRentalRequests_MachineryRentalRequestId",
                table: "MachineryRentals",
                column: "MachineryRentalRequestId",
                principalTable: "MachineryRentalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
