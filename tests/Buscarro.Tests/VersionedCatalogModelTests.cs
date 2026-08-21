using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Buscarro.Infrastructure.Services;

namespace Buscarro.Tests;

public sealed class VersionedCatalogModelTests
{
    [Fact]
    public void Model_maps_versioned_catalog_tables_keys_relationships_and_search_vector()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        var generation = model.FindEntityType(typeof(CatalogGeneration))!;
        var vehicle = model.FindEntityType(typeof(VehicleCatalogItem))!;
        var current = model.FindEntityType(typeof(ProviderCatalogCurrent))!;
        var publication = model.FindEntityType(typeof(CatalogPublication))!;

        Assert.Equal("catalog_generation", generation.GetTableName());
        Assert.Equal("vehicle_catalog", vehicle.GetTableName());
        Assert.Equal("provider_catalog_current", current.GetTableName());
        Assert.Equal("catalog_publication", publication.GetTableName());
        Assert.Contains(generation.GetKeys(), key => key.Properties.Select(p => p.Name).SequenceEqual(["Id", "Provider"]));
        Assert.Contains(vehicle.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(["GenerationId", "Provider", "ExternalId"]));
        Assert.Equal("tsvector", vehicle.FindProperty("SearchVector")!.GetColumnType());
        var searchIndex = vehicle.GetIndexes().Single(index =>
            index.Properties.Count == 1 && index.Properties[0].Name == "SearchVector");
        Assert.Equal("GIN", searchIndex.GetMethod(), ignoreCase: true);
        Assert.True(vehicle.FindProperty(nameof(VehicleCatalogItem.Year))!.IsNullable);
        Assert.True(vehicle.FindProperty(nameof(VehicleCatalogItem.Mileage))!.IsNullable);
        Assert.True(vehicle.FindProperty(nameof(VehicleCatalogItem.Price))!.IsNullable);
        AssertCheckContains(vehicle, "year IS NULL OR year >= 0");
        AssertCheckContains(vehicle, "mileage IS NULL OR mileage >= 0");
        AssertCheckContains(vehicle, "price IS NULL OR price >= 0");
        Assert.Contains(vehicle.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(["GenerationId", "StateNormalized", "Price", "Id"]));
        Assert.Contains(vehicle.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(["GenerationId", "StateNormalized", "Year", "Id"]));
        Assert.Contains(vehicle.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(["GenerationId", "BrandNormalized", "ModelNormalized", "VersionNormalized", "Price", "Id"]));

        var generationForeignKey = vehicle.GetForeignKeys().Single();
        Assert.Equal(DeleteBehavior.Cascade, generationForeignKey.DeleteBehavior);
        Assert.Equal(["GenerationId", "Provider"], generationForeignKey.Properties.Select(p => p.Name));

        Assert.All(current.GetForeignKeys().Where(fk => fk.PrincipalEntityType.ClrType == typeof(CatalogGeneration)),
            fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.All(model.GetEntityTypes().Where(type => type.ClrType != typeof(VehicleCatalogItem)),
            type => Assert.Null(type.FindProperty("SearchVector")));
    }

    [Fact]
    public void Extracted_configuration_preserves_legacy_table_names()
    {
        using var context = CreateContext();

        Assert.Equal("Users", context.Model.FindEntityType(typeof(User))!.GetTableName());
        Assert.Equal("FavoriteVehicles", context.Model.FindEntityType(typeof(FavoriteVehicle))!.GetTableName());
        Assert.Equal("VehicleProviderManufacturers", context.Model.FindEntityType(typeof(VehicleProviderManufacturer))!.GetTableName());
        Assert.Equal("VehicleProviderModels", context.Model.FindEntityType(typeof(VehicleProviderModel))!.GetTableName());
        Assert.Equal("VehicleProviderVersions", context.Model.FindEntityType(typeof(VehicleProviderVersion))!.GetTableName());
    }

    [Fact]
    public void Required_catalog_text_columns_reject_blank_values()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        AssertCheckContains(model.FindEntityType(typeof(CatalogGeneration))!, "btrim(provider) <> ''");
        AssertCheckContains(model.FindEntityType(typeof(VehicleCatalogItem))!, "btrim(provider) <> ''");
        AssertCheckContains(model.FindEntityType(typeof(VehicleCatalogItem))!, "btrim(external_id) <> ''");
        AssertCheckContains(model.FindEntityType(typeof(VehicleCatalogItem))!, "btrim(title) <> ''");
        AssertCheckContains(model.FindEntityType(typeof(VehicleCatalogItem))!, "btrim(title_normalized) <> ''");
        AssertCheckContains(model.FindEntityType(typeof(ProviderCatalogCurrent))!, "btrim(provider) <> ''");
    }

    [Fact]
    public void Binary_copy_contract_lists_nullable_numeric_columns_and_omits_generated_columns()
    {
        Assert.Contains("year", PostgresVehicleCatalogBatchWriter.CopySql);
        Assert.Contains("mileage", PostgresVehicleCatalogBatchWriter.CopySql);
        Assert.Contains("price", PostgresVehicleCatalogBatchWriter.CopySql);
        Assert.DoesNotContain("search_vector", PostgresVehicleCatalogBatchWriter.CopySql);
        Assert.DoesNotContain("(id,", PostgresVehicleCatalogBatchWriter.CopySql);
    }

    [Fact]
    public void Versioned_catalog_uses_bounded_integer_metadata_and_persists_names_only()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var generation = model.FindEntityType(typeof(CatalogGeneration))!;
        var vehicle = model.FindEntityType(typeof(VehicleCatalogItem))!;
        var current = model.FindEntityType(typeof(ProviderCatalogCurrent))!;
        var publication = model.FindEntityType(typeof(CatalogPublication))!;

        Assert.Equal(typeof(int), generation.FindProperty(nameof(CatalogGeneration.Id))!.ClrType);
        Assert.Equal(typeof(int), generation.FindProperty(nameof(CatalogGeneration.PageCount))!.ClrType);
        Assert.Equal(typeof(int), generation.FindProperty(nameof(CatalogGeneration.SourceItemCount))!.ClrType);
        Assert.Equal(typeof(int), generation.FindProperty(nameof(CatalogGeneration.ValidItemCount))!.ClrType);
        Assert.Equal(typeof(long), vehicle.FindProperty(nameof(VehicleCatalogItem.Id))!.ClrType);
        Assert.Equal(typeof(int), vehicle.FindProperty(nameof(VehicleCatalogItem.GenerationId))!.ClrType);
        Assert.Null(vehicle.FindProperty("ManufacturerCode"));
        Assert.Null(vehicle.FindProperty("ModelCode"));
        Assert.Null(vehicle.FindProperty("VersionCode"));
        Assert.Equal(typeof(int), current.FindProperty(nameof(ProviderCatalogCurrent.CurrentGenerationId))!.ClrType);
        Assert.Equal(typeof(int?), current.FindProperty(nameof(ProviderCatalogCurrent.LastPublicationId))!.ClrType);
        Assert.Equal(typeof(int), publication.FindProperty(nameof(CatalogPublication.Id))!.ClrType);
        Assert.Equal(typeof(int), publication.FindProperty(nameof(CatalogPublication.TotalCatalogCount))!.ClrType);
        Assert.DoesNotContain("manufacturer_code", PostgresVehicleCatalogBatchWriter.CopySql);
        Assert.DoesNotContain("model_code", PostgresVehicleCatalogBatchWriter.CopySql);
        Assert.DoesNotContain("version_code", PostgresVehicleCatalogBatchWriter.CopySql);
    }

    private static void AssertCheckContains(IEntityType entityType, string sqlFragment)
    {
        Assert.Contains(entityType.GetCheckConstraints(), constraint =>
            constraint.Sql.Contains(sqlFragment, StringComparison.Ordinal));
    }

    private static BuscarroDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BuscarroDbContext>()
            .UseNpgsql("Host=localhost;Database=buscarro_model;Username=test;Password=test")
            .Options;
        return new BuscarroDbContext(options);
    }
}
