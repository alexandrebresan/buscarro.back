namespace Buscarro.Infrastructure.Providers;

public sealed class CurrencyConversionOptions
{
    public string BaseUrl { get; init; } = "https://olinda.bcb.gov.br/";
    public string SourceCurrency { get; init; } = "USD";
    public string TargetCurrency { get; init; } = "BRL";
    public int MaximumRateAgeDays { get; init; } = 5;
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public bool IsValidForWorker() => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) && SourceCurrency == "USD" && TargetCurrency == "BRL" &&
        MaximumRateAgeDays == 5 && RequestTimeout > TimeSpan.Zero;
}
