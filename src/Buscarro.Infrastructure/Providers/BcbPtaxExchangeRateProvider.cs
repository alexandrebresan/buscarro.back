using System.Globalization;
using System.Text.Json;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Infrastructure.Contracts;
using Microsoft.Extensions.Options;

namespace Buscarro.Infrastructure.Providers;

public sealed class BcbPtaxExchangeRateProvider(HttpClient client, IOptions<CurrencyConversionOptions> options, TimeProvider timeProvider)
    : IExchangeRateProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ExchangeRateQuote> GetUsdToBrlQuoteAsync(CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var start = today.AddDays(-configuration.MaximumRateAgeDays);
        var initial = start.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
        var final = today.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
        var path = "olinda/servico/PTAX/versao/v1/odata/CotacaoDolarPeriodo(dataInicial=@dataInicial,dataFinalCotacao=@dataFinalCotacao)" +
            $"?@dataInicial='{initial}'&@dataFinalCotacao='{final}'&$format=json&$select=cotacaoVenda,dataHoraCotacao";
        using var deadline = new CancellationTokenSource(configuration.RequestTimeout, timeProvider);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        BcbPtaxContract payload;
        try
        {
            using var response = await client.GetAsync(path, timeout.Token);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            payload = await JsonSerializer.DeserializeAsync<BcbPtaxContract>(stream, JsonOptions, timeout.Token) ?? new();
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("BCB PTAX request exceeded its configured timeout.", exception);
        }
        var quote = (payload.Value ?? [])
            .Select(value => (value.CotacaoVenda, Timestamp: ParseTimestamp(value.DataHoraCotacao)))
            .Where(value => value.CotacaoVenda is > 0 && value.Timestamp is not null)
            .OrderByDescending(value => value.Timestamp)
            .FirstOrDefault();
        if (quote.CotacaoVenda is not > 0 || quote.Timestamp is null) throw new InvalidOperationException("bcb_ptax_quote_invalid");
        var quotedOn = DateOnly.FromDateTime(quote.Timestamp.Value.UtcDateTime);
        if (today.DayNumber - quotedOn.DayNumber > configuration.MaximumRateAgeDays) throw new InvalidOperationException("bcb_ptax_quote_stale");
        return new ExchangeRateQuote(quote.CotacaoVenda.Value, quotedOn);
    }

    private static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp)
            ? timestamp : null;
    }
}
