using System.Text.Json.Serialization;

namespace Buscarro.Infrastructure.Contracts;

public sealed class CarzinClassifiedSearchResponseContract
{
    [JsonPropertyName("current_results")]
    public int CurrentResults { get; set; }

    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }

    [JsonPropertyName("current_page")]
    public int CurrentPage { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("results")]
    public List<CarzinClassifiedContract> Results { get; set; } = [];
}

public sealed class CarzinClassifiedContract
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("manufacturer_id")]
    public int ManufacturerId { get; set; }

    [JsonPropertyName("model_id")]
    public int ModelId { get; set; }

    [JsonPropertyName("version_id")]
    public int VersionId { get; set; }

    [JsonPropertyName("year_manufacture")]
    public int YearManufacture { get; set; }

    [JsonPropertyName("year_model")]
    public int YearModel { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("mileage")]
    public int Mileage { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("fuel")]
    public string Fuel { get; set; } = string.Empty;

    [JsonPropertyName("gear")]
    public string Gear { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public sealed class CarzinManufacturersResponseContract
{
    [JsonPropertyName("manufacturers")]
    public List<CarzinManufacturerContract> Manufacturers { get; set; } = [];
}

public sealed class CarzinManufacturerContract
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public sealed class CarzinModelsResponseContract
{
    [JsonPropertyName("models")]
    public List<CarzinModelContract> Models { get; set; } = [];
}

public sealed class CarzinModelContract
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("year_min")]
    public int? YearMin { get; set; }

    [JsonPropertyName("year_max")]
    public int? YearMax { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public sealed class CarzinVersionsResponseContract
{
    [JsonPropertyName("versions")]
    public List<CarzinVersionContract> Versions { get; set; } = [];
}

public sealed class CarzinVersionContract
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
