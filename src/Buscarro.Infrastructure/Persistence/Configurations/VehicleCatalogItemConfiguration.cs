using Buscarro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Buscarro.Infrastructure.Persistence.Configurations;

internal sealed class VehicleCatalogItemConfiguration : IEntityTypeConfiguration<VehicleCatalogItem>
{
    public void Configure(EntityTypeBuilder<VehicleCatalogItem> entity)
    {
        entity.ToTable("vehicle_catalog", table =>
        {
            table.HasCheckConstraint("ck_vehicle_catalog_generation", "generation_id > 0");
            table.HasCheckConstraint("ck_vehicle_catalog_year", "year IS NULL OR year >= 0");
            table.HasCheckConstraint("ck_vehicle_catalog_mileage", "mileage IS NULL OR mileage >= 0");
            table.HasCheckConstraint("ck_vehicle_catalog_price", "price IS NULL OR price >= 0");
            table.HasCheckConstraint("ck_vehicle_catalog_required_text", "btrim(provider) <> '' AND btrim(external_id) <> '' AND btrim(title) <> '' AND btrim(title_normalized) <> ''");
        });
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(value => value.GenerationId).HasColumnName("generation_id");
        entity.Property(value => value.Provider).HasColumnName("provider").HasMaxLength(80).IsRequired();
        entity.Property(value => value.ExternalId).HasColumnName("external_id").HasMaxLength(160).IsRequired();
        MapText(entity.Property(value => value.Title), "title", 300);
        MapOptionalText(entity.Property(value => value.Brand), "brand", 120);
        MapOptionalText(entity.Property(value => value.Model), "model", 120);
        MapOptionalText(entity.Property(value => value.Version), "version", 160);
        entity.Property(value => value.Year).HasColumnName("year");
        entity.Property(value => value.Mileage).HasColumnName("mileage");
        entity.Property(value => value.Doors).HasColumnName("doors");
        entity.Property(value => value.Armored).HasColumnName("armored");
        MapOptionalText(entity.Property(value => value.Fuel), "fuel", 80);
        MapOptionalText(entity.Property(value => value.Transmission), "transmission", 80);
        MapOptionalText(entity.Property(value => value.Color), "color", 80);
        entity.Property(value => value.Price).HasColumnName("price").HasPrecision(18, 2);
        MapOptionalText(entity.Property(value => value.City), "city", 120);
        MapOptionalText(entity.Property(value => value.State), "state", 80);
        MapOptionalText(entity.Property(value => value.Url), "url", 1000);
        MapOptionalText(entity.Property(value => value.ImageUrl), "image_url", 1000);
        MapText(entity.Property(value => value.TitleNormalized), "title_normalized", 300);
        MapOptionalText(entity.Property(value => value.BrandNormalized), "brand_normalized", 120);
        MapOptionalText(entity.Property(value => value.ModelNormalized), "model_normalized", 120);
        MapOptionalText(entity.Property(value => value.VersionNormalized), "version_normalized", 160);
        MapOptionalText(entity.Property(value => value.FuelNormalized), "fuel_normalized", 80);
        MapOptionalText(entity.Property(value => value.TransmissionNormalized), "transmission_normalized", 80);
        MapOptionalText(entity.Property(value => value.ColorNormalized), "color_normalized", 80);
        MapOptionalText(entity.Property(value => value.CityNormalized), "city_normalized", 120);
        MapOptionalText(entity.Property(value => value.StateNormalized), "state_normalized", 80);
        entity.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql("to_tsvector('simple', coalesce(title_normalized, '') || ' ' || coalesce(brand_normalized, '') || ' ' || coalesce(model_normalized, '') || ' ' || coalesce(version_normalized, '') || ' ' || coalesce(city_normalized, '') || ' ' || coalesce(state_normalized, ''))", stored: true);
        entity.HasIndex("GenerationId", "Provider", "ExternalId").IsUnique()
            .HasDatabaseName("ux_vehicle_catalog_generation_provider_external");
        entity.HasIndex(value => new { value.Provider, value.ExternalId, value.GenerationId });
        entity.HasIndex(value => new { value.GenerationId, value.StateNormalized, value.Price, value.Id });
        entity.HasIndex(value => new { value.GenerationId, value.StateNormalized, value.Year, value.Id });
        entity.HasIndex(value => new { value.GenerationId, value.BrandNormalized, value.ModelNormalized, value.VersionNormalized, value.Price, value.Id });
        entity.HasIndex(value => new { value.GenerationId, value.CityNormalized });
        entity.HasIndex(value => new { value.GenerationId, value.FuelNormalized, value.TransmissionNormalized, value.ColorNormalized });
        entity.HasIndex("SearchVector")
            .HasAnnotation("Npgsql:IndexMethod", "gin")
            .HasDatabaseName("ix_vehicle_catalog_search_vector");
        entity.HasOne(value => value.Generation).WithMany(value => value.Items)
            .HasForeignKey(value => new { value.GenerationId, value.Provider })
            .HasPrincipalKey(value => new { value.Id, value.Provider })
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void MapText(PropertyBuilder<string> property, string columnName, int maxLength)
        => property.HasColumnName(columnName).HasMaxLength(maxLength).IsRequired();

    private static void MapOptionalText(PropertyBuilder<string?> property, string columnName, int maxLength)
        => property.HasColumnName(columnName).HasMaxLength(maxLength);
}
