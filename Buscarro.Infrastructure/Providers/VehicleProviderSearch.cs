using Buscarro.Application.DTOs;
using Buscarro.Domain.Entities;

namespace Buscarro.Infrastructure.Providers;

internal static class VehicleProviderSearch
{
    public static VehicleProviderResult Apply(
        IEnumerable<Vehicle> vehicles,
        VehicleSearchCriteria criteria)
    {
        var query = vehicles.AsEnumerable();

        query = FilterByText(query, vehicle => vehicle.Brand, criteria.Brand);
        query = FilterByText(query, vehicle => vehicle.Model, criteria.Model);
        query = FilterByText(query, vehicle => vehicle.Version, criteria.Version);
        query = FilterByText(query, vehicle => vehicle.Color, criteria.Color);
        query = FilterByText(query, vehicle => vehicle.Fuel, criteria.Fuel);
        query = FilterByText(query, vehicle => vehicle.Transmission, criteria.Transmission);
        query = FilterByText(query, vehicle => vehicle.State, criteria.State);
        query = FilterByText(query, vehicle => vehicle.City, criteria.City);

        if (criteria.PriceMin is not null)
        {
            query = query.Where(vehicle => vehicle.Price >= criteria.PriceMin.Value);
        }

        if (criteria.PriceMax is not null)
        {
            query = query.Where(vehicle => vehicle.Price <= criteria.PriceMax.Value);
        }

        if (criteria.YearMin is not null)
        {
            query = query.Where(vehicle => vehicle.Year >= criteria.YearMin.Value);
        }

        if (criteria.YearMax is not null)
        {
            query = query.Where(vehicle => vehicle.Year <= criteria.YearMax.Value);
        }

        query = criteria.Order?.Trim().ToLowerInvariant() switch
        {
            "price" => query.OrderBy(vehicle => vehicle.Price),
            "price-inverse" => query.OrderByDescending(vehicle => vehicle.Price),
            "year" => query.OrderBy(vehicle => vehicle.Year),
            "year-inverse" => query.OrderByDescending(vehicle => vehicle.Year),
            _ => query.OrderBy(vehicle => vehicle.Brand)
                .ThenBy(vehicle => vehicle.Model)
                .ThenByDescending(vehicle => vehicle.Year)
        };

        var filtered = query.ToArray();
        var page = Math.Max(1, criteria.Page);
        var results = Math.Max(1, criteria.Results);
        var paged = filtered
            .Skip((page - 1) * results)
            .Take(results)
            .ToArray();

        return new VehicleProviderResult
        {
            Vehicles = paged,
            CurrentResults = paged.Length,
            TotalResults = filtered.Length,
            CurrentPage = page,
            TotalPages = filtered.Length == 0
                ? 0
                : (int)Math.Ceiling(filtered.Length / (double)results)
        };
    }

    private static IEnumerable<Vehicle> FilterByText(
        IEnumerable<Vehicle> vehicles,
        Func<Vehicle, string> selector,
        string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return vehicles;
        }

        return vehicles.Where(vehicle =>
            string.Equals(selector(vehicle), expected.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
