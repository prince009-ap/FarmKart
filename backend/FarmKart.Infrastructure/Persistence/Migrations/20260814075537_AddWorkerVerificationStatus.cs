using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmKart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerVerificationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "WorkerProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Not Verified");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "WorkerProfiles");
        }
    }
}
