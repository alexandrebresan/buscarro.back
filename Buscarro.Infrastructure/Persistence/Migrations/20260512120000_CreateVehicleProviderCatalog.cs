using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buscarro.Infrastructure.Persistence.Migrations;

[Migration("20260512120000_CreateVehicleProviderCatalog")]
public partial class CreateVehicleProviderCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VehicleProviderManufacturers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProviderName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                ProviderCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                VehicleType = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleProviderManufacturers", manufacturer => manufacturer.Id);
            });

        migrationBuilder.CreateTable(
            name: "VehicleProviderModels",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ManufacturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProviderName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                ProviderCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                VehicleType = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleProviderModels", model => model.Id);
                table.ForeignKey(
                    name: "FK_VehicleProviderModels_VehicleProviderManufacturers_ManufacturerId",
                    column: model => model.ManufacturerId,
                    principalTable: "VehicleProviderManufacturers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "VehicleProviderVersions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProviderName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                ProviderCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                VehicleType = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleProviderVersions", version => version.Id);
                table.ForeignKey(
                    name: "FK_VehicleProviderVersions_VehicleProviderModels_ModelId",
                    column: version => version.ModelId,
                    principalTable: "VehicleProviderModels",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_VehicleProviderManufacturers_ProviderName_VehicleType_ProviderCode",
            table: "VehicleProviderManufacturers",
            columns: new[] { "ProviderName", "VehicleType", "ProviderCode" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VehicleProviderModels_ManufacturerId",
            table: "VehicleProviderModels",
            column: "ManufacturerId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleProviderModels_ProviderName_VehicleType_ManufacturerId_ProviderCode",
            table: "VehicleProviderModels",
            columns: new[] { "ProviderName", "VehicleType", "ManufacturerId", "ProviderCode" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VehicleProviderVersions_ModelId",
            table: "VehicleProviderVersions",
            column: "ModelId");

        migrationBuilder.CreateIndex(
            name: "IX_VehicleProviderVersions_ProviderName_VehicleType_ModelId_ProviderCode",
            table: "VehicleProviderVersions",
            columns: new[] { "ProviderName", "VehicleType", "ModelId", "ProviderCode" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VehicleProviderVersions");
        migrationBuilder.DropTable(name: "VehicleProviderModels");
        migrationBuilder.DropTable(name: "VehicleProviderManufacturers");
    }
}
