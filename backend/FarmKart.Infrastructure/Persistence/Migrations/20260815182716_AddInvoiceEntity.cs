using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmKart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AuctionOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SellerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SellerPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SellerLocation = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    BuyerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BuyerPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DeliveryOrPickupAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CropName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CropType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Variety = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrimaryImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    QuantityKg = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityMan = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PricePerMan = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SubtotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FulfillmentMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_AuctionOrders_AuctionOrderId",
                        column: x => x.AuctionOrderId,
                        principalTable: "AuctionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_CustomerProfiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_FarmerProfiles_FarmerProfileId",
                        column: x => x.FarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_AuctionOrderId",
                table: "Invoices",
                column: "AuctionOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerProfileId",
                table: "Invoices",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_FarmerProfileId",
                table: "Invoices",
                column: "FarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invoices");
        }
    }
}
