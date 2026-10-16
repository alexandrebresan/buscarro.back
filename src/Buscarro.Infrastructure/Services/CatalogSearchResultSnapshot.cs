using Buscarro.Application.DTOs;

namespace Buscarro.Infrastructure.Services;

internal static class CatalogSearchResultSnapshot
{
    // Both cache insertion and response creation own their DTO instances.
    internal static VehicleCatalogSearchResult Copy(VehicleCatalogSearchResult source, bool? fromCache = null) => new()
    {
        PublicationId = source.PublicationId,
        CatalogUpdatedAtUtc = source.CatalogUpdatedAtUtc,
        TotalCount = source.TotalCount,
        TotalPages = source.TotalPages,
        Page = source.Page,
        PageSize = source.PageSize,
        Vehicles = source.Vehicles.Select(vehicle => new VehicleResponse
        {
            ExternalId = vehicle.ExternalId,
            Provider = vehicle.Provider,
            Title = vehicle.Title,
            Brand = vehicle.Brand,
            Model = vehicle.Model,
            Version = vehicle.Version,
            Year = vehicle.Year,
            Mileage = vehicle.Mileage,
            Doors = vehicle.Doors,
            Armored = vehicle.Armored,
            Fuel = vehicle.Fuel,
            Transmission = vehicle.Transmission,
            Color = vehicle.Color,
            Price = vehicle.Price,
            City = vehicle.City,
            State = vehicle.State,
            Url = vehicle.Url,
            ImageUrl = vehicle.ImageUrl
        }).ToArray(),
        Providers = source.Providers.Select(provider => new ProviderStatusResponse
        {
            Provider = provider.Provider,
            Success = provider.Success,
            FromCache = fromCache ?? provider.FromCache,
            UsedStaleCache = provider.UsedStaleCache,
            Error = provider.Error,
            CurrentResults = provider.CurrentResults,
            CurrentPage = provider.CurrentPage
        }).ToArray()
    };
}
