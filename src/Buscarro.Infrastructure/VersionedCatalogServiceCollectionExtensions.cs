using Buscarro.Application.Interfaces;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Buscarro.Application.Services;
using Buscarro.Infrastructure.Services;
using Buscarro.Infrastructure.Providers;

namespace Buscarro.Infrastructure;

public static class VersionedCatalogServiceCollectionExtensions
{
    public static IServiceCollection AddCarzinCatalogNormalization(this IServiceCollection services)
    {
        services.AddScoped<ICarzinVehicleCatalogTaxonomyReader, EfCoreCarzinVehicleCatalogTaxonomyReader>();
        services.AddScoped<IVehicleCatalogItemNormalizer, CarzinVehicleCatalogItemNormalizer>();
        return services;
    }

    public static IServiceCollection AddVersionedCatalogPersistence(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, Buscarro.Infrastructure.UnitOfWork.UnitOfWork>();
        services.AddScoped<ICatalogGenerationRepository, CatalogGenerationRepository>();
        services.AddScoped<IVehicleCatalogWriteRepository, VehicleCatalogWriteRepository>();
        services.AddScoped<IProviderCatalogCurrentRepository, ProviderCatalogCurrentRepository>();
        services.AddScoped<ICatalogPublicationRepository, CatalogPublicationRepository>();
        services.AddScoped<ICatalogRetentionRepository, CatalogRetentionRepository>();
        services.AddScoped<ICatalogPublisher, PostgresCatalogPublisher>();
        return services;
    }

    public static IServiceCollection AddVersionedCatalogReads(this IServiceCollection services)
    {
        services.AddSingleton<VehicleCatalogSearchCriteriaNormalizer>();
        services.AddSingleton<CatalogQueryCache>();
        services.AddSingleton<IVehicleSearchReadService, PostgresVehicleCatalogReadService>();
        services.AddSingleton<IActiveVehicleCatalogReadService, PostgresActiveVehicleCatalogReadService>();
        return services;
    }
}
