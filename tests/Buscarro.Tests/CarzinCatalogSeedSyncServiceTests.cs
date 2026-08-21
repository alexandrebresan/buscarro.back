using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Buscarro.Tests;

public sealed class CarzinCatalogSeedSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_creates_manufacturer_model_and_version_hierarchy()
    {
        var store = new FakeCatalogStore(new DateTime(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc));
        var service = CreateService(store, new CatalogResponseHandler(new Dictionary<string, string>
        {
            ["/manufacturers"] = """{"results":[{"id":10,"name":"Honda"}]}""",
            ["/models"] = """{"results":[{"id":20,"name":"Civic"}]}""",
            ["/versions"] = """{"results":[{"id":30,"name":"Touring"}]}"""
        }));

        var result = await service.SyncAsync(CancellationToken.None);

        Assert.Equal(1, result.Manufacturers);
        Assert.Equal(1, result.Models);
        Assert.Equal(1, result.Versions);
        var manufacturer = Assert.Single(store.Manufacturers);
        var model = Assert.Single(store.Models);
        var version = Assert.Single(store.Versions);
        Assert.Equal("Honda", manufacturer.Name);
        Assert.Equal("Civic", model.Name);
        Assert.Equal("Touring", version.Name);
        Assert.Equal(manufacturer.Id, model.ManufacturerId);
        Assert.Equal(model.Id, version.ModelId);
        Assert.Equal(1, manufacturer.VehicleType);
        Assert.Equal(1, model.VehicleType);
        Assert.Equal(1, version.VehicleType);
        Assert.Equal(1, store.SaveChangesCalls);
    }

    [Fact]
    public async Task SyncAsync_updates_existing_catalog_entries_without_creating_duplicates()
    {
        var createdAtUtc = new DateTime(2026, 5, 17, 12, 0, 0, DateTimeKind.Utc);
        var updatedAtUtc = new DateTime(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc);
        var store = new FakeCatalogStore(updatedAtUtc);
        var manufacturer = store.AddManufacturer("10", "Marca antiga", createdAtUtc);
        var model = store.AddModel(manufacturer.Id, "20", "Modelo antigo", createdAtUtc);
        store.AddVersion(model.Id, "30", "Versao antiga", createdAtUtc);
        var service = CreateService(store, new CatalogResponseHandler(new Dictionary<string, string>
        {
            ["/manufacturers"] = """{"results":[{"id":10,"name":"Honda"}]}""",
            ["/models"] = """{"results":[{"id":20,"name":"Civic"}]}""",
            ["/versions"] = """{"results":[{"id":30,"name":"Touring"}]}"""
        }));

        await service.SyncAsync(CancellationToken.None);

        manufacturer = Assert.Single(store.Manufacturers);
        model = Assert.Single(store.Models);
        var version = Assert.Single(store.Versions);
        Assert.Equal("Honda", manufacturer.Name);
        Assert.Equal("Civic", model.Name);
        Assert.Equal("Touring", version.Name);
        Assert.Equal(createdAtUtc, manufacturer.CreatedAtUtc);
        Assert.Equal(createdAtUtc, model.CreatedAtUtc);
        Assert.Equal(createdAtUtc, version.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, manufacturer.UpdatedAtUtc);
        Assert.Equal(updatedAtUtc, model.UpdatedAtUtc);
        Assert.Equal(updatedAtUtc, version.UpdatedAtUtc);
    }

    [Fact]
    public async Task SyncAsync_saves_after_each_completed_manufacturer()
    {
        var store = new FakeCatalogStore(new DateTime(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc));
        var service = CreateService(store, new CatalogResponseHandler(new Dictionary<string, string>
        {
            ["/manufacturers"] = """{"results":[{"id":10,"name":"Honda"},{"id":11,"name":"Toyota"}]}""",
            ["/models"] = """{"results":[]}""",
            ["/versions"] = """{"results":[]}"""
        }));

        await service.SyncAsync(CancellationToken.None);

        Assert.Equal(2, store.SaveChangesCalls);
    }

    [Fact]
    public async Task SyncAsync_retries_catalog_request_when_provider_returns_transient_error()
    {
        var store = new FakeCatalogStore(new DateTime(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc));
        var handler = new TransientFailureThenSuccessHandler(
            "/manufacturers",
            HttpStatusCode.ServiceUnavailable,
            """{"results":[{"id":10,"name":"Honda"}]}""",
            new Dictionary<string, string>
            {
                ["/models"] = """{"results":[]}""",
                ["/versions"] = """{"results":[]}"""
            });
        var service = CreateService(store, handler);

        var result = await service.SyncAsync(CancellationToken.None);

        Assert.Equal(1, result.Manufacturers);
        Assert.Equal(2, handler.RequestCountByPath["/manufacturers"]);
    }

    [Fact]
    public async Task SyncAsync_fails_when_catalog_paths_are_not_configured()
    {
        var service = new CarzinCatalogSeedSyncService(
            new HttpClient(new CatalogResponseHandler(new Dictionary<string, string>())) { BaseAddress = new Uri("https://carzin.test") },
            Options.Create(new CarzinOptions { BaseUrl = "https://carzin.test", Token = "token" }),
            Options.Create(new CarzinCatalogOptions()),
            new FakeCatalogStore(DateTime.UtcNow),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SyncAsync(CancellationToken.None));

        Assert.Contains("CarzinCatalog", exception.Message);
    }

    [Fact]
    public void Api_startup_does_not_reference_carzin_catalog_seed_sync()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "Buscarro.Api", "Program.cs"));

        Assert.DoesNotContain("CarzinCatalogSeedSyncService", program);
    }

    private static CarzinCatalogSeedSyncService CreateService(
        FakeCatalogStore store,
        HttpMessageHandler handler)
    {
        return new CarzinCatalogSeedSyncService(
            new HttpClient(handler) { BaseAddress = new Uri("https://carzin.test") },
            Options.Create(new CarzinOptions { BaseUrl = "https://carzin.test", Token = "token" }),
            Options.Create(new CarzinCatalogOptions
            {
                ManufacturersPath = "/manufacturers",
                ModelsPath = "/models",
                VersionsPath = "/versions",
                RequestDelayMilliseconds = 0,
                MaxRetryAttempts = 5,
                RetryBaseDelaySeconds = 1,
                RetryMaxDelaySeconds = 30
            }),
            store,
            new FixedTimeProvider(store.UtcNow));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Buscarro.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class CatalogResponseHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, string> _responsesByPath;

        public CatalogResponseHandler(IReadOnlyDictionary<string, string> responsesByPath)
        {
            _responsesByPath = responsesByPath;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("name=token", body);
            Assert.Contains("name=type", body);

            if (request.RequestUri!.AbsolutePath == "/models")
            {
                Assert.Contains("name=manufacturer", body);
            }

            if (request.RequestUri!.AbsolutePath == "/versions")
            {
                Assert.Contains("name=manufacturer", body);
                Assert.Contains("name=model", body);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responsesByPath[request.RequestUri.AbsolutePath],
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed class TransientFailureThenSuccessHandler : HttpMessageHandler
    {
        private readonly string _transientFailurePath;
        private readonly HttpStatusCode _transientStatusCode;
        private readonly string _successBody;
        private readonly IReadOnlyDictionary<string, string> _responsesByPath;

        public TransientFailureThenSuccessHandler(
            string transientFailurePath,
            HttpStatusCode transientStatusCode,
            string successBody,
            IReadOnlyDictionary<string, string> responsesByPath)
        {
            _transientFailurePath = transientFailurePath;
            _transientStatusCode = transientStatusCode;
            _successBody = successBody;
            _responsesByPath = responsesByPath;
        }

        public Dictionary<string, int> RequestCountByPath { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            RequestCountByPath[path] = RequestCountByPath.GetValueOrDefault(path) + 1;

            if (path == _transientFailurePath && RequestCountByPath[path] == 1)
            {
                var transientResponse = new HttpResponseMessage(_transientStatusCode);
                transientResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return Task.FromResult(transientResponse);
            }

            var body = path == _transientFailurePath
                ? _successBody
                : _responsesByPath[path];

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FakeCatalogStore : ICarzinCatalogStore
    {
        private const string ProviderName = "Carzin";

        public FakeCatalogStore(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
        public int SaveChangesCalls { get; private set; }
        public List<VehicleProviderManufacturer> Manufacturers { get; } = [];
        public List<VehicleProviderModel> Models { get; } = [];
        public List<VehicleProviderVersion> Versions { get; } = [];

        public VehicleProviderManufacturer AddManufacturer(
            string providerCode,
            string name,
            DateTime createdAtUtc)
        {
            var manufacturer = new VehicleProviderManufacturer
            {
                ProviderName = ProviderName,
                ProviderCode = providerCode,
                Name = name,
                VehicleType = 1,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc
            };
            Manufacturers.Add(manufacturer);
            return manufacturer;
        }

        public VehicleProviderModel AddModel(
            Guid manufacturerId,
            string providerCode,
            string name,
            DateTime createdAtUtc)
        {
            var model = new VehicleProviderModel
            {
                ManufacturerId = manufacturerId,
                ProviderName = ProviderName,
                ProviderCode = providerCode,
                Name = name,
                VehicleType = 1,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc
            };
            Models.Add(model);
            return model;
        }

        public VehicleProviderVersion AddVersion(
            Guid modelId,
            string providerCode,
            string name,
            DateTime createdAtUtc)
        {
            var version = new VehicleProviderVersion
            {
                ModelId = modelId,
                ProviderName = ProviderName,
                ProviderCode = providerCode,
                Name = name,
                VehicleType = 1,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc
            };
            Versions.Add(version);
            return version;
        }

        public Task<VehicleProviderManufacturer> UpsertManufacturerAsync(
            string providerName,
            int vehicleType,
            string providerCode,
            string name,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var manufacturer = Manufacturers.FirstOrDefault(item =>
                item.ProviderName == providerName &&
                item.VehicleType == vehicleType &&
                item.ProviderCode == providerCode);

            if (manufacturer is null)
            {
                manufacturer = new VehicleProviderManufacturer
                {
                    ProviderName = providerName,
                    ProviderCode = providerCode,
                    VehicleType = vehicleType,
                    CreatedAtUtc = utcNow
                };
                Manufacturers.Add(manufacturer);
            }

            manufacturer.Name = name;
            manufacturer.UpdatedAtUtc = utcNow;
            return Task.FromResult(manufacturer);
        }

        public Task<VehicleProviderModel> UpsertModelAsync(
            VehicleProviderManufacturer manufacturer,
            string providerName,
            int vehicleType,
            string providerCode,
            string name,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var model = Models.FirstOrDefault(item =>
                item.ProviderName == providerName &&
                item.VehicleType == vehicleType &&
                item.ManufacturerId == manufacturer.Id &&
                item.ProviderCode == providerCode);

            if (model is null)
            {
                model = new VehicleProviderModel
                {
                    ManufacturerId = manufacturer.Id,
                    ProviderName = providerName,
                    ProviderCode = providerCode,
                    VehicleType = vehicleType,
                    CreatedAtUtc = utcNow
                };
                Models.Add(model);
            }

            model.Name = name;
            model.UpdatedAtUtc = utcNow;
            return Task.FromResult(model);
        }

        public Task<VehicleProviderVersion> UpsertVersionAsync(
            VehicleProviderModel model,
            string providerName,
            int vehicleType,
            string providerCode,
            string name,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var version = Versions.FirstOrDefault(item =>
                item.ProviderName == providerName &&
                item.VehicleType == vehicleType &&
                item.ModelId == model.Id &&
                item.ProviderCode == providerCode);

            if (version is null)
            {
                version = new VehicleProviderVersion
                {
                    ModelId = model.Id,
                    ProviderName = providerName,
                    ProviderCode = providerCode,
                    VehicleType = vehicleType,
                    CreatedAtUtc = utcNow
                };
                Versions.Add(version);
            }

            version.Name = name;
            version.UpdatedAtUtc = utcNow;
            return Task.FromResult(version);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTime now)
        {
            _now = new DateTimeOffset(now, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }
}
