using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Buscarro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionedVehicleCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_generation",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ready_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    page_count = table.Column<int>(type: "integer", nullable: false),
                    source_item_count = table.Column<int>(type: "integer", nullable: false),
                    valid_item_count = table.Column<int>(type: "integer", nullable: false),
                    invalid_item_count = table.Column<int>(type: "integer", nullable: false),
                    duplicate_item_count = table.Column<int>(type: "integer", nullable: false),
                    checksum_sha256 = table.Column<string>(type: "character(64)", maxLength: 64, nullable: true),
                    validation_summary_json = table.Column<string>(type: "jsonb", nullable: true),
                    sanitized_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_generation", x => x.id);
                    table.UniqueConstraint("AK_catalog_generation_id_provider", x => new { x.id, x.provider });
                    table.CheckConstraint("ck_catalog_generation_checksum", "checksum_sha256 IS NULL OR length(checksum_sha256) = 64");
                    table.CheckConstraint("ck_catalog_generation_counts", "page_count >= 0 AND source_item_count >= 0 AND valid_item_count >= 0 AND invalid_item_count >= 0 AND duplicate_item_count >= 0");
                    table.CheckConstraint("ck_catalog_generation_provider_not_blank", "btrim(provider) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "catalog_publication",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total_catalog_count = table.Column<int>(type: "integer", nullable: false),
                    provider_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_publication", x => x.id);
                    table.CheckConstraint("ck_catalog_publication_counts", "total_catalog_count >= 0 AND provider_count >= 0");
                });

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

            migrationBuilder.CreateTable(
                name: "vehicle_catalog",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    generation_id = table.Column<int>(type: "integer", nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    external_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    version = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    year = table.Column<int>(type: "integer", nullable: true),
                    mileage = table.Column<int>(type: "integer", nullable: true),
                    doors = table.Column<int>(type: "integer", nullable: true),
                    armored = table.Column<bool>(type: "boolean", nullable: true),
                    fuel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    transmission = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    color = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    state = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    title_normalized = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    brand_normalized = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    model_normalized = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    version_normalized = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    fuel_normalized = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    transmission_normalized = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    color_normalized = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    city_normalized = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    state_normalized = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('simple', coalesce(title_normalized, '') || ' ' || coalesce(brand_normalized, '') || ' ' || coalesce(model_normalized, '') || ' ' || coalesce(version_normalized, '') || ' ' || coalesce(city_normalized, '') || ' ' || coalesce(state_normalized, ''))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_catalog", x => x.id);
                    table.CheckConstraint("ck_vehicle_catalog_generation", "generation_id > 0");
                    table.CheckConstraint("ck_vehicle_catalog_mileage", "mileage IS NULL OR mileage >= 0");
                    table.CheckConstraint("ck_vehicle_catalog_price", "price IS NULL OR price >= 0");
                    table.CheckConstraint("ck_vehicle_catalog_required_text", "btrim(provider) <> '' AND btrim(external_id) <> '' AND btrim(title) <> '' AND btrim(title_normalized) <> ''");
                    table.CheckConstraint("ck_vehicle_catalog_year", "year IS NULL OR year >= 0");
                    table.ForeignKey(
                        name: "FK_vehicle_catalog_catalog_generation_generation_id_provider",
                        columns: x => new { x.generation_id, x.provider },
                        principalTable: "catalog_generation",
                        principalColumns: new[] { "id", "provider" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_catalog_current",
                columns: table => new
                {
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    current_generation_id = table.Column<int>(type: "integer", nullable: false),
                    previous_generation_id = table.Column<int>(type: "integer", nullable: true),
                    last_publication_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_catalog_current", x => x.provider);
                    table.CheckConstraint("ck_provider_catalog_current_distinct", "previous_generation_id IS NULL OR current_generation_id <> previous_generation_id");
                    table.CheckConstraint("ck_provider_catalog_current_provider_not_blank", "btrim(provider) <> ''");
                    table.ForeignKey(
                        name: "FK_provider_catalog_current_catalog_generation_current_generat~",
                        columns: x => new { x.current_generation_id, x.provider },
                        principalTable: "catalog_generation",
                        principalColumns: new[] { "id", "provider" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_provider_catalog_current_catalog_generation_previous_genera~",
                        columns: x => new { x.previous_generation_id, x.provider },
                        principalTable: "catalog_generation",
                        principalColumns: new[] { "id", "provider" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_provider_catalog_current_catalog_publication_last_publicati~",
                        column: x => x.last_publication_id,
                        principalTable: "catalog_publication",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateIndex(
                name: "ix_catalog_generation_provider_status_created_at",
                table: "catalog_generation",
                columns: new[] { "provider", "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_catalog_publication_published_at",
                table: "catalog_publication",
                column: "published_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_provider_catalog_current_current_generation_id",
                table: "provider_catalog_current",
                column: "current_generation_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_catalog_current_current_generation_id_provider",
                table: "provider_catalog_current",
                columns: new[] { "current_generation_id", "provider" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_catalog_current_last_publication_id",
                table: "provider_catalog_current",
                column: "last_publication_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_catalog_current_previous_generation_id",
                table: "provider_catalog_current",
                column: "previous_generation_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_catalog_current_previous_generation_id_provider",
                table: "provider_catalog_current",
                columns: new[] { "previous_generation_id", "provider" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_catalog_generation_id_brand_normalized_model_normal~",
                table: "vehicle_catalog",
                columns: new[] { "generation_id", "brand_normalized", "model_normalized", "version_normalized", "price", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_catalog_generation_id_city_normalized",
                table: "vehicle_catalog",
                columns: new[] { "generation_id", "city_normalized" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_catalog_generation_id_fuel_normalized_transmission_~",
                table: "vehicle_catalog",
                columns: new[] { "generation_id", "fuel_normalized", "transmission_normalized", "color_normalized" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_catalog_generation_id_state_normalized_price_id",
                table: "vehicle_catalog",
                columns: new[] { "generation_id", "state_normalized", "price", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_catalog_generation_id_state_normalized_year_id",
                table: "vehicle_catalog",
                columns: new[] { "generation_id", "state_normalized", "year", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_catalog_provider_external_id_generation_id",
                table: "vehicle_catalog",
                columns: new[] { "provider", "external_id", "generation_id" });

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_catalog_search_vector",
                table: "vehicle_catalog",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ux_vehicle_catalog_generation_provider_external",
                table: "vehicle_catalog",
                columns: new[] { "generation_id", "provider", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_catalog_current");

            migrationBuilder.DropTable(
                name: "vehicle_catalog");

            migrationBuilder.DropTable(
                name: "VehicleProviderAttributeValues");

            migrationBuilder.DropTable(
                name: "catalog_publication");

            migrationBuilder.DropTable(
                name: "catalog_generation");
        }
    }
}
