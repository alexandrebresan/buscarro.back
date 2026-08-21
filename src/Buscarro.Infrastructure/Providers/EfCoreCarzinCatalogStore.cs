using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Infrastructure.Providers;

public sealed class EfCoreCarzinCatalogStore : ICarzinCatalogStore
{
    private readonly BuscarroDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public EfCoreCarzinCatalogStore(
        BuscarroDbContext dbContext,
        IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<VehicleProviderManufacturer> UpsertManufacturerAsync(
        string providerName,
        int vehicleType,
        string providerCode,
        string name,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var manufacturer = await _dbContext.VehicleProviderManufacturers
            .FirstOrDefaultAsync(item =>
                item.ProviderName == providerName &&
                item.VehicleType == vehicleType &&
                item.ProviderCode == providerCode,
                cancellationToken);

        if (manufacturer is null)
        {
            manufacturer = new VehicleProviderManufacturer
            {
                ProviderName = providerName,
                ProviderCode = providerCode,
                VehicleType = vehicleType,
                CreatedAtUtc = utcNow
            };
            await _dbContext.VehicleProviderManufacturers.AddAsync(manufacturer, cancellationToken);
        }

        manufacturer.Name = name;
        manufacturer.UpdatedAtUtc = utcNow;
        return manufacturer;
    }

    public async Task<VehicleProviderModel> UpsertModelAsync(
        VehicleProviderManufacturer manufacturer,
        string providerName,
        int vehicleType,
        string providerCode,
        string name,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var model = await _dbContext.VehicleProviderModels
            .FirstOrDefaultAsync(item =>
                item.ProviderName == providerName &&
                item.VehicleType == vehicleType &&
                item.ManufacturerId == manufacturer.Id &&
                item.ProviderCode == providerCode,
                cancellationToken);

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
            await _dbContext.VehicleProviderModels.AddAsync(model, cancellationToken);
        }

        model.Name = name;
        model.UpdatedAtUtc = utcNow;
        return model;
    }

    public async Task<VehicleProviderVersion> UpsertVersionAsync(
        VehicleProviderModel model,
        string providerName,
        int vehicleType,
        string providerCode,
        string name,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var version = await _dbContext.VehicleProviderVersions
            .FirstOrDefaultAsync(item =>
                item.ProviderName == providerName &&
                item.VehicleType == vehicleType &&
                item.ModelId == model.Id &&
                item.ProviderCode == providerCode,
                cancellationToken);

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
            await _dbContext.VehicleProviderVersions.AddAsync(version, cancellationToken);
        }

        version.Name = name;
        version.UpdatedAtUtc = utcNow;
        return version;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
