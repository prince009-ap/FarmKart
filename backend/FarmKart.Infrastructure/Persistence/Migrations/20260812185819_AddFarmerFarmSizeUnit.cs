using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmKart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Adds explicit farm-size unit storage for farmer profiles.
    /// Existing rows keep FarmSizeUnit = NULL because pre-migration values were stored
    /// without a recorded unit and must not be silently reinterpreted as Vigha.
    /// New registrations set FarmSizeUnit = Vigha (1).
    /// </summary>
    public partial class AddFarmerFarmSizeUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FarmSizeUnit",
                table: "FarmerProfiles",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FarmSizeUnit",
                table: "FarmerProfiles");
        }
    }
}
