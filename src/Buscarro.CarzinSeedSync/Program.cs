using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

var configuration = SeedSyncConfiguration.Load(AppContext.BaseDirectory);
var connectionString = PostgresConnectionString.Normalize(
    configuration.Get("DATABASE_URL")
        ?? configuration.Get("ConnectionStrings:BuscarroDb")
        ?? string.Empty);
var carzinOptions = new CarzinOptions
{
    BaseUrl = configuration.Require("Carzin:BaseUrl"),
    Token = configuration.Require("Carzin:Token")
};
var catalogOptions = new CarzinCatalogOptions
{
    ManufacturersPath = configuration.Require("CarzinCatalog:ManufacturersPath"),
    ModelsPath = configuration.Require("CarzinCatalog:ModelsPath"),
    VersionsPath = configuration.Require("CarzinCatalog:VersionsPath"),
    RequestDelayMilliseconds = configuration.GetInt(
        "CarzinCatalog:RequestDelayMilliseconds",
        new CarzinCatalogOptions().RequestDelayMilliseconds),
    MaxRetryAttempts = configuration.GetInt(
        "CarzinCatalog:MaxRetryAttempts",
        new CarzinCatalogOptions().MaxRetryAttempts),
    RetryBaseDelaySeconds = configuration.GetInt(
        "CarzinCatalog:RetryBaseDelaySeconds",
        new CarzinCatalogOptions().RetryBaseDelaySeconds),
    RetryMaxDelaySeconds = configuration.GetInt(
        "CarzinCatalog:RetryMaxDelaySeconds",
        new CarzinCatalogOptions().RetryMaxDelaySeconds)
};

var dbContextOptions = new DbContextOptionsBuilder<BuscarroDbContext>()
    .UseNpgsql(
        connectionString,
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 8,
            maxRetryDelay: TimeSpan.FromSeconds(60),
            errorCodesToAdd: null))
    .Options;

await using var dbContext = new BuscarroDbContext(dbContextOptions);
using var httpClient = new HttpClient
{
    BaseAddress = new Uri(carzinOptions.BaseUrl)
};

var unitOfWork = new UnitOfWork(dbContext);
var store = new EfCoreCarzinCatalogStore(dbContext, unitOfWork);
var service = new CarzinCatalogSeedSyncService(
    httpClient,
    Options.Create(carzinOptions),
    Options.Create(catalogOptions),
    store,
    TimeProvider.System);

var result = await service.SyncAsync(CancellationToken.None);

Console.WriteLine(
    $"Carzin catalog sync concluido. Manufacturers: {result.Manufacturers}; Models: {result.Models}; Versions: {result.Versions}.");

internal sealed class SeedSyncConfiguration
{
    private readonly IReadOnlyDictionary<string, string> _values;

    private SeedSyncConfiguration(IReadOnlyDictionary<string, string> values)
    {
        _values = values;
    }

    public static SeedSyncConfiguration Load(string basePath)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LoadJson(Path.Combine(basePath, "appsettings.json"), values);
        LoadJson(Path.Combine(basePath, "appsettings.Development.json"), values);
        LoadEnvironment(values);
        return new SeedSyncConfiguration(values);
    }

    public string Require(string key)
    {
        var value = Get(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} nao configurado.");
        }

        return value.Trim();
    }

    public string? Get(string key)
    {
        return _values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    public int GetInt(string key, int defaultValue)
    {
        var value = Get(key);

        return int.TryParse(value, out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    private static void LoadJson(string path, IDictionary<string, string> values)
    {
        if (!File.Exists(path))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        ReadObject(document.RootElement, string.Empty, values);
    }

    private static void ReadObject(
        JsonElement element,
        string prefix,
        IDictionary<string, string> values)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var key = string.IsNullOrWhiteSpace(prefix)
                ? property.Name
                : $"{prefix}:{property.Name}";

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                ReadObject(property.Value, key, values);
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.String)
            {
                values[key] = property.Value.GetString() ?? string.Empty;
                continue;
            }

            values[key] = property.Value.ToString();
        }
    }

    private static void LoadEnvironment(IDictionary<string, string> values)
    {
        foreach (var key in new[]
        {
            "ConnectionStrings__BuscarroDb",
            "DATABASE_URL",
            "Carzin__BaseUrl",
            "Carzin__Token",
            "CarzinCatalog__ManufacturersPath",
            "CarzinCatalog__ModelsPath",
            "CarzinCatalog__VersionsPath",
            "CarzinCatalog__RequestDelayMilliseconds",
            "CarzinCatalog__MaxRetryAttempts",
            "CarzinCatalog__RetryBaseDelaySeconds",
            "CarzinCatalog__RetryMaxDelaySeconds"
        })
        {
            var value = Environment.GetEnvironmentVariable(key);

            if (!string.IsNullOrWhiteSpace(value))
            {
                values[key.Replace("__", ":", StringComparison.Ordinal)] = value;
            }
        }
    }
}
