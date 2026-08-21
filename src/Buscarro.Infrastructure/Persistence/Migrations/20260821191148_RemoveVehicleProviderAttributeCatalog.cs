using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Buscarro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVehicleProviderAttributeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleProviderAttributeValues");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleProviderAttributeValues",
                columns: table => new
                {
                    ProviderName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AttributeType = table.Column<short>(type: "smallint", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleProviderAttributeValues", x => new { x.ProviderName, x.AttributeType, x.ProviderCode });
                    table.CheckConstraint("CK_VehicleProviderAttributeValues_AttributeType", "\"AttributeType\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_VehicleProviderAttributeValues_RequiredText", "btrim(\"ProviderName\") <> '' AND btrim(\"ProviderCode\") <> '' AND btrim(\"Name\") <> ''");
                });

            migrationBuilder.InsertData(
                table: "VehicleProviderAttributeValues",
                columns: new[] { "AttributeType", "ProviderCode", "ProviderName", "Name" },
                values: new object[,]
                {
                    { (short)1, "1", "Carzin", "amarelo" },
                    { (short)1, "10", "Carzin", "marrom" },
                    { (short)1, "11", "Carzin", "prata" },
                    { (short)1, "12", "Carzin", "preto" },
                    { (short)1, "13", "Carzin", "rosa" },
                    { (short)1, "14", "Carzin", "roxo" },
                    { (short)1, "15", "Carzin", "verde" },
                    { (short)1, "16", "Carzin", "vermelho" },
                    { (short)1, "17", "Carzin", "vinho" },
                    { (short)1, "2", "Carzin", "azul" },
                    { (short)1, "3", "Carzin", "bege" },
                    { (short)1, "4", "Carzin", "branco" },
                    { (short)1, "5", "Carzin", "bronze" },
                    { (short)1, "6", "Carzin", "cinza" },
                    { (short)1, "7", "Carzin", "dourado" },
                    { (short)1, "8", "Carzin", "indefinida" },
                    { (short)1, "9", "Carzin", "nada" },
                    { (short)2, "1", "Carzin", "automático" },
                    { (short)2, "2", "Carzin", "automático / sequencial" },
                    { (short)2, "3", "Carzin", "automático cvt" },
                    { (short)2, "4", "Carzin", "manual" },
                    { (short)2, "5", "Carzin", "semi-automático" },
                    { (short)3, "1", "Carzin", "gasolina" },
                    { (short)3, "10", "Carzin", "elétrico" },
                    { (short)3, "2", "Carzin", "álcool" },
                    { (short)3, "3", "Carzin", "Diesel" },
                    { (short)3, "4", "Carzin", "gás natural" },
                    { (short)3, "5", "Carzin", "gasolina e álcool" },
                    { (short)3, "6", "Carzin", "gasolina e gás natural" },
                    { (short)3, "7", "Carzin", "álcool e gás natural" },
                    { (short)3, "8", "Carzin", "gasolina, álcool e GNV" },
                    { (short)3, "9", "Carzin", "elétrico e gasolina" }
                });
        }
    }
}
