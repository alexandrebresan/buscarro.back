namespace Buscarro.Domain.Entities;

public sealed class VehicleCatalogItem
{
    private VehicleCatalogItem()
    {
    }

    public VehicleCatalogItem(
        int generationId,
        string provider,
        string externalId,
        string title,
        string titleNormalized)
    {
        if (generationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generationId));
        }

        GenerationId = generationId;
        Provider = RequireText(provider, nameof(provider));
        ExternalId = RequireText(externalId, nameof(externalId));
        Title = RequireText(title, nameof(title));
        TitleNormalized = RequireText(titleNormalized, nameof(titleNormalized));
    }

    public long Id { get; private set; }
    public int GenerationId { get; private set; }
    public CatalogGeneration? Generation { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ExternalId { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Version { get; set; }
    public int? Year { get; set; }
    public int? Mileage { get; set; }
    public int? Doors { get; set; }
    public bool? Armored { get; set; }
    public string? Fuel { get; set; }
    public string? Transmission { get; set; }
    public string? Color { get; set; }
    public decimal? Price { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
    public string TitleNormalized { get; private set; } = string.Empty;
    public string? BrandNormalized { get; set; }
    public string? ModelNormalized { get; set; }
    public string? VersionNormalized { get; set; }
    public string? FuelNormalized { get; set; }
    public string? TransmissionNormalized { get; set; }
    public string? ColorNormalized { get; set; }
    public string? CityNormalized { get; set; }
    public string? StateNormalized { get; set; }

    private static string RequireText(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}
