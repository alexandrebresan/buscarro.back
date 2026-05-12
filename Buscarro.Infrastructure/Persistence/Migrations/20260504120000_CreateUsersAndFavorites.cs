using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buscarro.Infrastructure.Persistence.Migrations;

public partial class CreateUsersAndFavorites : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", user => user.Id);
            });

        migrationBuilder.CreateTable(
            name: "FavoriteVehicles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ExternalId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Provider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Brand = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Model = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Version = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                Mileage = table.Column<int>(type: "int", nullable: false),
                Fuel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Transmission = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Color = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                State = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FavoriteVehicles", favorite => favorite.Id);
                table.ForeignKey(
                    name: "FK_FavoriteVehicles_Users_UserId",
                    column: favorite => favorite.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FavoriteVehicles_UserId_Provider_ExternalId",
            table: "FavoriteVehicles",
            columns: new[] { "UserId", "Provider", "ExternalId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FavoriteVehicles");
        migrationBuilder.DropTable(name: "Users");
    }
}
