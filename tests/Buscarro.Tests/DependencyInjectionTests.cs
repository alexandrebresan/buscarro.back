using Buscarro.Api.Configurations;
using Buscarro.Api.Controllers;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Handlers;
using Buscarro.Application.Services;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Repositories;
using Buscarro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Buscarro.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AiSearch_controller_only_depends_on_mediator()
    {
        var constructor = Assert.Single(typeof(AiSearchController).GetConstructors());
        var parameter = Assert.Single(constructor.GetParameters());

        Assert.Equal(typeof(MediatR.IMediator), parameter.ParameterType);
    }

    [Fact]
    public async Task AddAiSearch_registers_the_complete_ai_search_composition()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OpenAI:BaseUrl"] =
                        "https://api.openai.test/",
                    ["OpenAI:Model"] = "test-model"
                })
            .Build();

        services.AddMemoryCache();
        services.AddDbContextFactory<BuscarroDbContext>(
            options => options.UseInMemoryDatabase(
                Guid.NewGuid().ToString()));
        services.AddAiSearch(configuration);

        using var serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        AssertSingleton<SemanticProfileCatalog>(serviceProvider);
        var detector =
            AssertSingleton<ISemanticProfileDetector>(serviceProvider);
        Assert.IsType<SemanticProfileDetector>(detector);
        AssertSingleton<VehicleSearchPromptContentAnalyzer>(
            serviceProvider);
        AssertSingleton<VehicleSearchCriteriaCanonicalizer>(serviceProvider);
        AssertSingleton<OpenAiVehicleSearchCriteriaMapper>(serviceProvider);
        AssertSingleton<VehicleSearchCriteriaMerger>(serviceProvider);
        AssertSingleton<VehicleSearchUrlBuilder>(serviceProvider);
        var taxonomyProvider = AssertSingleton<IVehicleSearchTaxonomyProvider>(serviceProvider);
        Assert.Equal("StaticVehicleSearchTaxonomyProvider", taxonomyProvider.GetType().Name);
        var taxonomy = await taxonomyProvider.GetAsync(TestContext.Current.CancellationToken);
        Assert.Contains("prata", taxonomy.Colors);
        Assert.Contains("manual", taxonomy.Gears);
        Assert.Contains("gasolina e álcool", taxonomy.Fuels);
        Assert.Same(
            TimeProvider.System,
            AssertSingleton<TimeProvider>(serviceProvider));

        var options = serviceProvider
            .GetRequiredService<IOptions<OpenAiOptions>>()
            .Value;
        Assert.Equal("https://api.openai.test/", options.BaseUrl);
        Assert.Equal("test-model", options.Model);
        Assert.NotNull(
            serviceProvider.GetRequiredService<IHttpClientFactory>());

        var firstInterpreter =
            serviceProvider.GetRequiredService<IAiPromptInterpreter>();
        var secondInterpreter =
            serviceProvider.GetRequiredService<IAiPromptInterpreter>();
        Assert.IsType<AiPromptInterpreter>(firstInterpreter);
        Assert.IsType<AiPromptInterpreter>(secondInterpreter);
        Assert.NotSame(firstInterpreter, secondInterpreter);
    }

    [Fact]
    public void Catalog_read_service_registration_can_be_validated_with_scopes()
    {
        var services = new ServiceCollection();
        const string connectionString = "Host=localhost;Database=BuscarroTestDb;Username=postgres;Password=postgres";

        services.AddDbContext<BuscarroDbContext>(options =>
            options.UseNpgsql(connectionString),
            optionsLifetime: ServiceLifetime.Singleton);
        services.AddDbContextFactory<BuscarroDbContext>(options =>
            options.UseNpgsql(connectionString));
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

    [Fact]
    public void Versioned_catalog_reads_register_postgres_as_the_only_search_service()
    {
        var services = new ServiceCollection();

        services.AddVersionedCatalogReads();

        var registration = Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IVehicleSearchReadService));
        Assert.Equal(
            typeof(PostgresVehicleCatalogReadService),
            registration.ImplementationType);
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(Microsoft.Extensions.Hosting.IHostedService));
    }

    [Fact]
    public void Favorite_handler_composition_resolves_with_postgres_catalog_reads()
    {
        var services = new ServiceCollection();
        services.AddSingleton(NpgsqlDataSource.Create(
            "Host=localhost;Database=BuscarroTestDb;Username=postgres;Password=postgres"));
        services.AddOptions<CatalogQueryOptions>();
        services.AddVersionedCatalogReads();
        services.AddScoped<AddFavoriteVehicleHandler>();
        services.AddScoped<ICurrentUserService, FakeCurrentUserService>();
        services.AddScoped<IFavoriteVehicleRepository, FakeFavoriteVehicleRepository>();
        services.AddScoped<IUnitOfWork, FakeUnitOfWork>();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AddFavoriteVehicleHandler>());
    }

    private static TService AssertSingleton<TService>(
        IServiceProvider serviceProvider)
        where TService : class
    {
        var first = serviceProvider.GetRequiredService<TService>();
        var second = serviceProvider.GetRequiredService<TService>();

        Assert.Same(first, second);
        return first;
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
    }

    private sealed class FakeFavoriteVehicleRepository : IFavoriteVehicleRepository
    {
        public Task<IReadOnlyCollection<FavoriteVehicle>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<FavoriteVehicle>>([]);
        public Task<FavoriteVehicle?> GetByIdAndUserIdAsync(Guid id, Guid userId, CancellationToken cancellationToken) => Task.FromResult<FavoriteVehicle?>(null);
        public Task<bool> ExistsAsync(Guid userId, string provider, string externalId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(FavoriteVehicle favorite, CancellationToken cancellationToken) => Task.CompletedTask;
        public void Remove(FavoriteVehicle favorite) { }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }
}
