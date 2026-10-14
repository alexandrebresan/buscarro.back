using System.Text.Json.Serialization;

namespace Buscarro.Infrastructure.Contracts;

public sealed class BcbPtaxContract
{
    [JsonPropertyName("value")] public IReadOnlyCollection<BcbPtaxQuoteContract>? Value { get; init; }
}

public sealed class BcbPtaxQuoteContract
{
    [JsonPropertyName("cotacaoVenda")] public decimal? CotacaoVenda { get; init; }
    [JsonPropertyName("dataHoraCotacao")] public string? DataHoraCotacao { get; init; }
}
