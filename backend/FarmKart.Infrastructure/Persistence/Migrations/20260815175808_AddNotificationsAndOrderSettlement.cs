using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmKart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsAndOrderSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedAuctionId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedOrderId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSettled",
                table: "AuctionOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettledAtUtc",
                table: "AuctionOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettledQuantityKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SettledAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SettlementStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderSettlements_AuctionOrders_AuctionOrderId",
                        column: x => x.AuctionOrderId,
                        principalTable: "AuctionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderSettlements_AuctionId",
                table: "OrderSettlements",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderSettlements_AuctionOrderId",
                table: "OrderSettlements",
                column: "AuctionOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderSettlements_CustomerProfileId",
                table: "OrderSettlements",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderSettlements_FarmerProfileId",
                table: "OrderSettlements",
                column: "FarmerProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderSettlements");

            migrationBuilder.DropColumn(
                name: "RelatedAuctionId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RelatedOrderId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsSettled",
                table: "AuctionOrders");

            migrationBuilder.DropColumn(
                name: "SettledAtUtc",
                table: "AuctionOrders");
        }
    }
}
