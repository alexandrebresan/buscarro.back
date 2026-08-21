using Buscarro.Application.Interfaces;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Buscarro.Infrastructure.Providers;

namespace Buscarro.Tests;

public sealed class VersionedCatalogDependencyInjectionTests
{
    [Fact]
    public void Carzin_catalog_normalization_registers_reader_and_normalizer_as_scoped()
    {
        var services = new ServiceCollection();
        services.AddDbContext<BuscarroDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddCarzinCatalogNormalization();

        Assert.Equal(ServiceLifetime.Scoped,
            services.Single(value => value.ServiceType == typeof(ICarzinVehicleCatalogTaxonomyReader)).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped,
            services.Single(value => value.ServiceType == typeof(IVehicleCatalogItemNormalizer)).Lifetime);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.IsType<CarzinVehicleCatalogItemNormalizer>(
            scope.ServiceProvider.GetRequiredService<IVehicleCatalogItemNormalizer>());
    }

    [Fact]
    public void Versioned_catalog_services_are_registered_as_scoped()
    {
        var services = new ServiceCollection();
        services.AddDbContext<BuscarroDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddVersionedCatalogPersistence();

        var serviceTypes = new[]
        {
            typeof(ICatalogGenerationRepository),
            typeof(IVehicleCatalogWriteRepository),
            typeof(IProviderCatalogCurrentRepository),
            typeof(ICatalogPublicationRepository),
            typeof(ICatalogRetentionRepository),
            typeof(IUnitOfWork),
            typeof(ICatalogPublisher)
        };
        Assert.All(serviceTypes, serviceType =>
            Assert.Equal(ServiceLifetime.Scoped, services.Single(value => value.ServiceType == serviceType).Lifetime));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.All(serviceTypes, serviceType => Assert.NotNull(scope.ServiceProvider.GetService(serviceType)));
    }
}
