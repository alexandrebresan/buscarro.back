using System.Text.Json.Serialization;

namespace Buscarro.Infrastructure.Contracts;

public sealed class OlxVehiclesResponseContract
{
    [JsonPropertyName("listings")]
    public List<OlxVehicleContract> Listings { get; set; } = [];
}

public sealed class OlxVehicleContract
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("car_name")]
    public string CarName { get; set; } = string.Empty;

    [JsonPropertyName("automaker")]
    public string Automaker { get; set; } = string.Empty;

    [JsonPropertyName("model_name")]
    public string ModelName { get; set; } = string.Empty;

    [JsonPropertyName("trim")]
    public string Trim { get; set; } = string.Empty;

    [JsonPropertyName("model_year")]
    public int ModelYear { get; set; }

    [JsonPropertyName("km")]
    public int Kilometers { get; set; }

    [JsonPropertyName("fuel")]
    public string Fuel { get; set; } = string.Empty;

    [JsonPropertyName("gearbox")]
    public string Gearbox { get; set; } = string.Empty;

    [JsonPropertyName("paint")]
    public string Paint { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;
}
