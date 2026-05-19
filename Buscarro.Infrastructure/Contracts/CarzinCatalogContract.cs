using System.Text.Json.Serialization;

namespace Buscarro.Infrastructure.Contracts;

public sealed class CarzinCatalogItemContract
{
    public string ProviderCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class CarzinCatalogListContract
{
    [JsonPropertyName("results")]
    public List<CarzinCatalogItemContract> Results { get; init; } = [];
}
