using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Buscarro.Tests;

public sealed class VehicleProviderAttributeCatalogTests
{
    [Fact]
    public void Current_model_no_longer_maps_provider_attribute_catalog()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        Assert.Null(model.FindEntityType("Buscarro.Domain.Entities.VehicleProviderAttributeValue"));
        Assert.NotNull(model.FindEntityType("Buscarro.Domain.Entities.VehicleProviderManufacturer"));
        Assert.NotNull(model.FindEntityType("Buscarro.Domain.Entities.VehicleProviderModel"));
        Assert.NotNull(model.FindEntityType("Buscarro.Domain.Entities.VehicleProviderVersion"));
        Assert.NotNull(model.FindEntityType("Buscarro.Domain.Entities.VehicleCatalogItem"));
    }

    [Fact]
    public void Removal_migration_only_drops_provider_attribute_catalog()
    {
        using var context = CreateContext();
        var migrations = context.GetService<IMigrationsAssembly>();
        var migrationType = Assert.Single(migrations.Migrations,
            item => item.Key.EndsWith("_RemoveVehicleProviderAttributeCatalog", StringComparison.Ordinal)).Value;
        var migration = (Migration)Activator.CreateInstance(migrationType.AsType())!;

        var drop = Assert.Single(migration.UpOperations.OfType<DropTableOperation>());
        Assert.Equal("VehicleProviderAttributeValues", drop.Name);
        Assert.Single(migration.UpOperations);

        var recreate = Assert.Single(migration.DownOperations.OfType<CreateTableOperation>());
        Assert.Equal("VehicleProviderAttributeValues", recreate.Name);
        var seed = Assert.Single(migration.DownOperations.OfType<InsertDataOperation>());
        Assert.Equal("VehicleProviderAttributeValues", seed.Table);
        Assert.Equal(32, seed.Values.GetLength(0));
    }

    private static BuscarroDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BuscarroDbContext>()
            .UseNpgsql("Host=localhost;Database=buscarro_model;Username=test;Password=test")
            .Options;
        return new BuscarroDbContext(options);
    }
}
