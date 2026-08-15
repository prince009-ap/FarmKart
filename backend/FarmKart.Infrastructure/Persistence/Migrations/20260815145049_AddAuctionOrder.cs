using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmKart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuctionOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedQuantityKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PricePerMan = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionOrders", x => x.Id);
                    table.CheckConstraint("CK_AuctionOrder_Amounts_NonNegative", "[AllocatedQuantityKg] >= 0 AND [PricePerMan] >= 0 AND [TotalAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_AuctionOrders_AuctionAllocations_AuctionAllocationId",
                        column: x => x.AuctionAllocationId,
                        principalTable: "AuctionAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionOrders_AuctionPayments_AuctionPaymentId",
                        column: x => x.AuctionPaymentId,
                        principalTable: "AuctionPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionOrders_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionOrders_Crops_CropId",
                        column: x => x.CropId,
                        principalTable: "Crops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionOrders_CustomerProfiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionOrders_FarmerProfiles_FarmerProfileId",
                        column: x => x.FarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionOrders_AuctionAllocationId",
                table: "AuctionOrders",
                column: "AuctionAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionOrders_AuctionId",
                table: "AuctionOrders",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionOrders_AuctionPaymentId",
                table: "AuctionOrders",
                column: "AuctionPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionOrders_CropId",
                table: "AuctionOrders",
                column: "CropId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionOrders_CustomerProfileId",
                table: "AuctionOrders",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionOrders_FarmerProfileId",
                table: "AuctionOrders",
                column: "FarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionOrders_OrderNumber",
                table: "AuctionOrders",
                column: "OrderNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuctionOrders");
        }
    }
}
