using System.Net;
using Buscarro.Infrastructure.Providers;
using Microsoft.Extensions.Options;

namespace Buscarro.Tests;

public sealed class BcbPtaxExchangeRateProviderTests
{
    [Fact]
    public async Task Uses_the_latest_positive_ptax_quote()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"value":[{"cotacaoVenda":5.1,"dataHoraCotacao":"2026-08-20T12:00:00Z"},{"cotacaoVenda":5.2,"dataHoraCotacao":"2026-08-22T12:00:00Z"}]}""") });
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 22, 15, 0, 0, TimeSpan.Zero));
        var provider = new BcbPtaxExchangeRateProvider(new HttpClient(handler) { BaseAddress = new Uri("https://olinda.bcb.gov.br/") }, Options.Create(new CurrencyConversionOptions()), clock);
        var quote = await provider.GetUsdToBrlQuoteAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5.2m, quote.UsdToBrlRate); Assert.Equal(new DateOnly(2026, 8, 22), quote.QuotedOn);
    }
    [Theory]
    [InlineData("{\"value\":[]}")]
    [InlineData("{\"value\":[{\"cotacaoVenda\":0,\"dataHoraCotacao\":\"2026-08-22T12:00:00Z\"}]}")]
    public async Task Rejects_empty_or_nonpositive_ptax_quotes(string json)
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 22, 15, 0, 0, TimeSpan.Zero));
        var provider = new BcbPtaxExchangeRateProvider(new HttpClient(handler) { BaseAddress = new Uri("https://olinda.bcb.gov.br/") }, Options.Create(new CurrencyConversionOptions()), clock);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetUsdToBrlQuoteAsync(TestContext.Current.CancellationToken));
        Assert.Equal("bcb_ptax_quote_invalid", exception.Message);
    }

    [Fact]
    public async Task Rejects_stale_ptax_quote()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"value\":[{\"cotacaoVenda\":5.1,\"dataHoraCotacao\":\"2026-08-16T12:00:00Z\"}]}") });
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 22, 15, 0, 0, TimeSpan.Zero));
        var provider = new BcbPtaxExchangeRateProvider(new HttpClient(handler) { BaseAddress = new Uri("https://olinda.bcb.gov.br/") }, Options.Create(new CurrencyConversionOptions()), clock);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetUsdToBrlQuoteAsync(TestContext.Current.CancellationToken));
        Assert.Equal("bcb_ptax_quote_stale", exception.Message);
    }
    [Fact]
    public async Task Parses_the_real_bcb_timestamp_without_timezone()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"value\":[{\"cotacaoVenda\":5.1,\"dataHoraCotacao\":\"2026-08-22 12:34:56.789\"}]}") });
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 22, 15, 0, 0, TimeSpan.Zero));
        var provider = new BcbPtaxExchangeRateProvider(new HttpClient(handler) { BaseAddress = new Uri("https://olinda.bcb.gov.br/") }, Options.Create(new CurrencyConversionOptions()), clock);
        var quote = await provider.GetUsdToBrlQuoteAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new DateOnly(2026, 8, 22), quote.QuotedOn);
    }

    [Fact]
    public async Task Internal_timeout_is_distinct_from_caller_cancellation_without_retry()
    {
        var sends = 0;
        var timeoutProvider = new BcbPtaxExchangeRateProvider(new HttpClient(new BlockingHandler(() => sends++)) { BaseAddress = new Uri("https://olinda.bcb.gov.br/") },
            Options.Create(new CurrencyConversionOptions { RequestTimeout = TimeSpan.FromMilliseconds(20) }), TimeProvider.System);
        await Assert.ThrowsAsync<TimeoutException>(() => timeoutProvider.GetUsdToBrlQuoteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, sends);

        var callerSends = 0;
        var callerProvider = new BcbPtaxExchangeRateProvider(new HttpClient(new BlockingHandler(() => callerSends++)) { BaseAddress = new Uri("https://olinda.bcb.gov.br/") }, Options.Create(new CurrencyConversionOptions()), TimeProvider.System);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callerProvider.GetUsdToBrlQuoteAsync(cancelled.Token));
        Assert.Equal(1, callerSends);
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(handle(request)); }
    private sealed class BlockingHandler(Func<int> sent) : HttpMessageHandler { protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { sent(); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return new HttpResponseMessage(HttpStatusCode.OK); } }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
