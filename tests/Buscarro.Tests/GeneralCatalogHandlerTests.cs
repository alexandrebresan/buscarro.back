using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;

namespace Buscarro.Tests;

public sealed class GeneralCatalogHandlerTests
{
    [Fact]
    public async Task ListManufacturersHandler_returns_catalog_from_read_service()
    {
        var service = new FakeVehicleProviderCatalogReadService(
            manufacturers:
            [
                new ManufacturerCatalogItemResponse("10", "Honda", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))
            ]);
        var handler = new ListManufacturersHandler(service);

        var result = await handler.Handle(new ListManufacturersQuery(), CancellationToken.None);

        var manufacturer = Assert.Single(result);
        Assert.Equal("10", manufacturer.ProviderCode);
        Assert.Equal("Honda", manufacturer.Name);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), manufacturer.ManufacturerId);
    }

    [Fact]
    public async Task ListModelsHandler_rejects_empty_manufacturer_id()
    {
        var handler = new ListModelsHandler(new FakeVehicleProviderCatalogReadService());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new ListModelsQuery(null), CancellationToken.None));

        Assert.Equal("manufacturer_id e obrigatorio.", exception.Message);
    }

    [Fact]
    public async Task ListModelsHandler_uses_manufacturer_database_id()
    {
        var manufacturerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = new FakeVehicleProviderCatalogReadService(
            models:
            [
                new ModelCatalogItemResponse("1", "Civic", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))
            ]);
        var handler = new ListModelsHandler(service);

        var result = await handler.Handle(new ListModelsQuery(manufacturerId), CancellationToken.None);

        Assert.Equal(manufacturerId, service.LastManufacturerId);
        var model = Assert.Single(result);
        Assert.Equal("1", model.ProviderCode);
        Assert.Equal("Civic", model.Name);
        Assert.Equal(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), model.ModelId);
    }

    [Fact]
    public async Task ListVersionsHandler_rejects_empty_model_id()
    {
        var handler = new ListVersionsHandler(new FakeVehicleProviderCatalogReadService());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new ListVersionsQuery(null), CancellationToken.None));

        Assert.Equal("model_id e obrigatorio.", exception.Message);
    }

    [Fact]
    public async Task ListVersionsHandler_uses_model_database_id()
    {
        var modelId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var service = new FakeVehicleProviderCatalogReadService(
            versions:
            [
                new VersionCatalogItemResponse("100", "EXL")
            ]);
        var handler = new ListVersionsHandler(service);

        var result = await handler.Handle(new ListVersionsQuery(modelId), CancellationToken.None);

        Assert.Equal(modelId, service.LastModelId);
        var version = Assert.Single(result);
        Assert.Equal("100", version.ProviderCode);
        Assert.Equal("EXL", version.Name);
    }

    private sealed class FakeVehicleProviderCatalogReadService : IVehicleProviderCatalogReadService
    {
        private readonly IReadOnlyCollection<ManufacturerCatalogItemResponse> _manufacturers;
        private readonly IReadOnlyCollection<ModelCatalogItemResponse> _models;
        private readonly IReadOnlyCollection<VersionCatalogItemResponse> _versions;

        public FakeVehicleProviderCatalogReadService(
            IReadOnlyCollection<ManufacturerCatalogItemResponse>? manufacturers = null,
            IReadOnlyCollection<ModelCatalogItemResponse>? models = null,
            IReadOnlyCollection<VersionCatalogItemResponse>? versions = null)
        {
            _manufacturers = manufacturers ?? [];
            _models = models ?? [];
            _versions = versions ?? [];
        }

        public Guid? LastManufacturerId { get; private set; }
        public Guid? LastModelId { get; private set; }

        public Task<IReadOnlyCollection<ManufacturerCatalogItemResponse>> ListManufacturersAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_manufacturers);
        }

        public Task<IReadOnlyCollection<ModelCatalogItemResponse>> ListModelsAsync(
            Guid manufacturerId,
            CancellationToken cancellationToken)
        {
            LastManufacturerId = manufacturerId;
            return Task.FromResult(_models);
        }

        public Task<IReadOnlyCollection<VersionCatalogItemResponse>> ListVersionsAsync(
            Guid modelId,
            CancellationToken cancellationToken)
        {
            LastModelId = modelId;
            return Task.FromResult(_versions);
        }
    }
}
