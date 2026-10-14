using System.Text.Json.Serialization;

namespace Buscarro.Infrastructure.Contracts;

public sealed class AutoDevListingsContract
{
    [JsonPropertyName("data")] public IReadOnlyCollection<AutoDevListingContract>? Data { get; init; }
    [JsonPropertyName("links")] public AutoDevLinksContract? Links { get; init; }
}

public sealed class AutoDevLinksContract
{
    [JsonPropertyName("next")] public string? Next { get; init; }
}

public sealed class AutoDevListingContract
{
    [JsonPropertyName("vin")] public string? Vin { get; init; }
    [JsonPropertyName("vehicle")] public AutoDevVehicleContract? Vehicle { get; init; }
    [JsonPropertyName("retailListing")] public AutoDevRetailListingContract? RetailListing { get; init; }
}

public sealed class AutoDevVehicleContract
{
    [JsonPropertyName("vin")] public string? Vin { get; init; }
    [JsonPropertyName("year")] public int? Year { get; init; }
    [JsonPropertyName("make")] public string? Make { get; init; }
    [JsonPropertyName("model")]
    [JsonConverter(typeof(AutoDevStringOrNumberJsonConverter))]
    public string? Model { get; init; }
    [JsonPropertyName("trim")]
    [JsonConverter(typeof(AutoDevStringOrNumberJsonConverter))]
    public string? Trim { get; init; }
    [JsonPropertyName("miles")] public int? Miles { get; init; }
    [JsonPropertyName("doors")] public int? Doors { get; init; }
    [JsonPropertyName("fuel")]
    [JsonConverter(typeof(AutoDevStringOrNumberJsonConverter))]
    public string? Fuel { get; init; }
    [JsonPropertyName("transmission")]
    [JsonConverter(typeof(AutoDevStringOrNumberJsonConverter))]
    public string? Transmission { get; init; }
    [JsonPropertyName("exteriorColor")]
    [JsonConverter(typeof(AutoDevStringOrNumberJsonConverter))]
    public string? ExteriorColor { get; init; }
}

public sealed class AutoDevRetailListingContract
{
    [JsonPropertyName("price")] public decimal? Price { get; init; }
    [JsonPropertyName("miles")] public int? Miles { get; init; }
    [JsonPropertyName("city")] public string? City { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("vdp")] public string? Vdp { get; init; }
    [JsonPropertyName("primaryImage")] public string? PrimaryImage { get; init; }
}
