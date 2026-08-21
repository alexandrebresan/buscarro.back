namespace Buscarro.Application.DTOs;

public sealed record CatalogSourceVehicle(
    string? ExternalId,
    string? Title,
    string? Brand,
    string? Model,
    string? Version,
    string? ManufacturerCode,
    string? ModelCode,
    string? VersionCode,
    int? Year,
    int? Mileage,
    int? Doors,
    bool? Armored,
    string? Fuel,
    string? Transmission,
    string? Color,
    decimal? Price,
    string? City,
    string? State,
    string? Url,
    string? ImageUrl);
