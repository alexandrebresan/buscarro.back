using Buscarro.Application.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Buscarro.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void Catalog_read_service_registration_can_be_validated_with_scopes()
    {
        var services = new ServiceCollection();
        const string connectionString = "Server=(localdb)\\mssqllocaldb;Database=BuscarroTestDb;Trusted_Connection=True;";

        services.AddDbContext<BuscarroDbContext>(options =>
            options.UseSqlServer(connectionString),
            optionsLifetime: ServiceLifetime.Singleton);
        services.AddDbContextFactory<BuscarroDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddMemoryCache();
        services.AddSingleton<IVehicleProviderCatalogReadService, VehicleProviderCatalogReadService>();

        var exception = Record.Exception(() => services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            }));

        Assert.Null(exception);
    }
}
