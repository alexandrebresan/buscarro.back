using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buscarro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleFavoriteDoorsAndArmored : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Armored",
                table: "FavoriteVehicles",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Doors",
                table: "FavoriteVehicles",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Armored",
                table: "FavoriteVehicles");

            migrationBuilder.DropColumn(
                name: "Doors",
                table: "FavoriteVehicles");
        }
    }
}
