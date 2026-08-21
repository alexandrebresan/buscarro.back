using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Tests;

public sealed class EfCoreCarzinVehicleCatalogTaxonomyReaderTests
{
    [Fact]
    public async Task Snapshot_filters_provider_vehicle_type_and_parent_hierarchy_without_tracking()
    {
        await using var db = CreateContext();
        var validManufacturer = Manufacturer("Carzin", 1, "1");
        var wrongTypeManufacturer = Manufacturer("Carzin", 2, "2");
        var validModel = Model(validManufacturer, 1, "10");
        var wrongParentModel = Model(wrongTypeManufacturer, 1, "20");
        db.AddRange(validManufacturer, wrongTypeManufacturer, validModel, wrongParentModel,
            Version(validModel, 1, "100"), Version(wrongParentModel, 1, "200"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var snapshot = await new EfCoreCarzinVehicleCatalogTaxonomyReader(db)
            .ReadSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal("1", Assert.Single(snapshot.Manufacturers).Code);
        Assert.Equal("10", Assert.Single(snapshot.Models).Code);
        Assert.Equal("100", Assert.Single(snapshot.Versions).Code);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Snapshot_honors_caller_cancellation()
    {
        await using var db = CreateContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new EfCoreCarzinVehicleCatalogTaxonomyReader(db).ReadSnapshotAsync(cancellation.Token));
    }

    private static BuscarroDbContext CreateContext()
        => new(new DbContextOptionsBuilder<BuscarroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static VehicleProviderManufacturer Manufacturer(string provider, int type, string code)
        => new() { ProviderName = provider, VehicleType = type, ProviderCode = code, Name = $"M-{code}" };

    private static VehicleProviderModel Model(VehicleProviderManufacturer manufacturer, int type, string code)
        => new()
        {
            Manufacturer = manufacturer, ManufacturerId = manufacturer.Id, ProviderName = "Carzin",
            VehicleType = type, ProviderCode = code, Name = $"Model-{code}"
        };

    private static VehicleProviderVersion Version(VehicleProviderModel model, int type, string code)
        => new()
        {
            Model = model, ModelId = model.Id, ProviderName = "Carzin", VehicleType = type,
            ProviderCode = code, Name = $"Version-{code}"
        };
}
