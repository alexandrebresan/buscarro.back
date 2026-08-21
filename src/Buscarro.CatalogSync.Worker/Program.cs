using Buscarro.Application.DTOs;
using Buscarro.Application.Commands;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using Buscarro.Application.Services;
using Buscarro.Application.Handlers;
using Buscarro.CatalogSync.Worker;
using Buscarro.Infrastructure;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using MediatR;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
CatalogRollbackArguments? rollbackArguments;
try { rollbackArguments=CatalogRollbackArguments.Parse(args); }
catch(ArgumentException) { Console.Error.WriteLine("{\"status\":\"error\",\"reason\":\"invalid_arguments\"}");Environment.ExitCode=2;return; }
var connectionString = PostgresConnectionString.Normalize(
    builder.Configuration["DATABASE_URL"] ?? builder.Configuration.GetConnectionString("BuscarroDb") ?? string.Empty);
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("DATABASE_URL or ConnectionStrings:BuscarroDb is required.");

builder.Services.AddOptions<CatalogWorkerOptions>().Bind(builder.Configuration.GetSection(CatalogWorkerOptions.SectionName))
    .Validate(value => value.IsValid(), "CatalogWorker intervals and providers must be valid.").ValidateOnStart();
builder.Services.AddOptions<CarzinOptions>().Bind(builder.Configuration.GetSection("Carzin"))
    .Validate(value => Uri.TryCreate(value.BaseUrl, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(value.Token), "Carzin BaseUrl and Token are required.").ValidateOnStart();
builder.Services.AddOptions<CatalogSynchronizationOptions>().Bind(builder.Configuration.GetSection("CatalogSynchronization"))
    .Validate(value => value.PageSize > 0 && value.BatchSize > 0 && value.MaxPages > 0 && value.MaxVehicles > 0 &&
        value.MaximumDropPercentage is >= 0 and <= 100 &&
        value.Timeout > TimeSpan.Zero && value.FailureRecordTimeout > TimeSpan.Zero && value.AbandonedLoadingGrace > TimeSpan.Zero &&
        value.ReadyRetention > TimeSpan.Zero && value.FailedRetention > TimeSpan.Zero, "CatalogSynchronization limits must be positive.").ValidateOnStart();
builder.Services.AddSingleton(services => services.GetRequiredService<IOptions<CatalogSynchronizationOptions>>().Value);
builder.Services.AddOptions<CarzinCatalogSourceOptions>().Bind(builder.Configuration.GetSection("CarzinCatalogSource"))
    .Validate(value => value.IsValidForWorker(), "CarzinCatalogSource requires exactly three retries and valid delay/circuit settings.").ValidateOnStart();
builder.Services.AddSingleton(services => services.GetRequiredService<IOptions<CarzinCatalogSourceOptions>>().Value);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CatalogSyncHealthState>();
builder.Services.AddSingleton<CarzinCircuitBreaker>();
builder.Services.AddSingleton<ICarzinRetryDelay, CarzinRetryDelay>();
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddDbContext<BuscarroDbContext>(options => options.UseNpgsql(connectionString), optionsLifetime: ServiceLifetime.Singleton);
builder.Services.AddDbContextFactory<BuscarroDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddVersionedCatalogPersistence();
builder.Services.AddCarzinCatalogNormalization();
builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(FindAllVehiclesQuery).Assembly));
var workerHandlers = new HashSet<Type> { typeof(SynchronizeProviderCatalogHandler), typeof(CleanupCatalogHandler),typeof(RollbackCatalogHandler) };
for (var index = builder.Services.Count - 1; index >= 0; index--)
{
    var descriptor = builder.Services[index];
    if (descriptor.ServiceType.IsGenericType &&
        descriptor.ServiceType.GetGenericTypeDefinition() == typeof(MediatR.IRequestHandler<,>) &&
        descriptor.ImplementationType is not null && !workerHandlers.Contains(descriptor.ImplementationType))
        builder.Services.RemoveAt(index);
}
builder.Services.AddScoped<ICatalogSyncValidator, CatalogSyncValidator>();
builder.Services.AddScoped<IProviderSynchronizationLock, PostgresProviderSynchronizationLock>();
builder.Services.AddScoped<IVehicleCatalogBatchWriter, PostgresVehicleCatalogBatchWriter>();
builder.Services.AddScoped<ICatalogGenerationFailureRecorder, CatalogGenerationFailureRecorder>();
builder.Services.AddHttpClient<CarzinCatalogSource>((services, client) =>
    client.BaseAddress = new Uri(services.GetRequiredService<IOptions<CarzinOptions>>().Value.BaseUrl));
builder.Services.AddScoped<IVehicleCatalogSource>(services => services.GetRequiredService<CarzinCatalogSource>());
if(rollbackArguments is null)
{
    builder.Services.AddHostedService<CatalogSynchronizationWorker>();
    builder.Services.AddHostedService<CatalogCleanupWorker>();
}

using var host=builder.Build();
if(rollbackArguments is null)await host.RunAsync();
else
{
    try
    {
        await using var scope=host.Services.CreateAsyncScope();
        var result=await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new RollbackCatalogCommand(rollbackArguments.Provider,rollbackArguments.TargetGenerationId));
        Console.Out.WriteLine(JsonSerializer.Serialize(result));
    }
    catch(Exception)
    {
        Console.Error.WriteLine("{\"status\":\"error\",\"reason\":\"rollback_failed\"}");
        Environment.ExitCode=1;
    }
}
