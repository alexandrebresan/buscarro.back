using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

var configuration = SeedSyncConfiguration.Load(AppContext.BaseDirectory);
var connectionString = configuration.Require("ConnectionStrings:BuscarroDb");
var carzinOptions = new CarzinOptions
{
    BaseUrl = configuration.Require("Carzin:BaseUrl"),
    Token = configuration.Require("Carzin:Token")
};
var catalogOptions = new CarzinCatalogOptions
{
    ManufacturersPath = configuration.Require("CarzinCatalog:ManufacturersPath"),
    ModelsPath = configuration.Require("CarzinCatalog:ModelsPath"),
    VersionsPath = configuration.Require("CarzinCatalog:VersionsPath")
};

var dbContextOptions = new DbContextOptionsBuilder<BuscarroDbContext>()
    .UseSqlServer(connectionString)
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
        if (!_values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} nao configurado.");
        }

        return value.Trim();
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
            "Carzin__BaseUrl",
            "Carzin__Token",
            "CarzinCatalog__ManufacturersPath",
            "CarzinCatalog__ModelsPath",
            "CarzinCatalog__VersionsPath"
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
