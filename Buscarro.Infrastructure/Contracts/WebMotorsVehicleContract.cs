using System.Text.Json.Serialization;

namespace Buscarro.Infrastructure.Contracts;

public sealed class WebMotorsVehiclesResponseContract
{
    [JsonPropertyName("SearchResults")]
    public List<WebMotorsVehicleContract> SearchResults { get; set; } = [];
}

public sealed class WebMotorsVehicleContract
{
    [JsonPropertyName("ID")]
    public string ListingCode { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Make")]
    public string Make { get; set; } = string.Empty;

    [JsonPropertyName("Model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("Version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("FabricationYear")]
    public int FabricationYear { get; set; }

    [JsonPropertyName("Odometer")]
    public int Odometer { get; set; }

    [JsonPropertyName("FuelType")]
    public string FuelType { get; set; } = string.Empty;

    [JsonPropertyName("TransmissionType")]
    public string TransmissionType { get; set; } = string.Empty;

    [JsonPropertyName("ColorName")]
    public string ColorName { get; set; } = string.Empty;

    [JsonPropertyName("Price")]
    public decimal Price { get; set; }

    [JsonPropertyName("SellerCity")]
    public string SellerCity { get; set; } = string.Empty;

    [JsonPropertyName("SellerState")]
    public string SellerState { get; set; } = string.Empty;

    [JsonPropertyName("VehicleUrl")]
    public string VehicleUrl { get; set; } = string.Empty;
}
