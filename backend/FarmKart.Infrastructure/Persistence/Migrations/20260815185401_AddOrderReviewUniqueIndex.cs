using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmKart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReviewUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Reviews_RelatedEntityType_RelatedEntityId",
                table: "Reviews",
                columns: new[] { "RelatedEntityType", "RelatedEntityId" },
                unique: true,
                filter: "[RelatedEntityId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_RelatedEntityType_RelatedEntityId",
                table: "Reviews");
        }
    }
}
