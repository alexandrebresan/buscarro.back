using System.Text.Json.Serialization;

namespace Buscarro.Infrastructure.Contracts;

public sealed class ICarrosVehiclesResponseContract
{
    [JsonPropertyName("vehicles")]
    public List<ICarrosVehicleContract> Vehicles { get; set; } = [];
}

public sealed class ICarrosVehicleContract
{
    [JsonPropertyName("ad_id")]
    public string AdId { get; set; } = string.Empty;

    [JsonPropertyName("nome_carro")]
    public string NomeCarro { get; set; } = string.Empty;

    [JsonPropertyName("brand_name")]
    public string BrandName { get; set; } = string.Empty;

    [JsonPropertyName("model_name")]
    public string ModelName { get; set; } = string.Empty;

    [JsonPropertyName("derivative")]
    public string Derivative { get; set; } = string.Empty;

    [JsonPropertyName("production_year")]
    public int ProductionYear { get; set; }

    [JsonPropertyName("mileage_value")]
    public int MileageValue { get; set; }

    [JsonPropertyName("fuel_label")]
    public string FuelLabel { get; set; } = string.Empty;

    [JsonPropertyName("transmission_label")]
    public string TransmissionLabel { get; set; } = string.Empty;

    [JsonPropertyName("color_label")]
    public string ColorLabel { get; set; } = string.Empty;

    [JsonPropertyName("price_value")]
    public decimal PriceValue { get; set; }

    [JsonPropertyName("location_city")]
    public string LocationCity { get; set; } = string.Empty;

    [JsonPropertyName("location_state")]
    public string LocationState { get; set; } = string.Empty;

    [JsonPropertyName("detail_url")]
    public string DetailUrl { get; set; } = string.Empty;
}
