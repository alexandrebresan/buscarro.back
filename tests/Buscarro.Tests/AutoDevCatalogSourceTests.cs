using Buscarro.Infrastructure.Providers;
using Buscarro.Application.DTOs;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Buscarro.Tests;

public sealed class AutoDevCatalogSourceTests
{
    [Fact]
    public void Options_require_the_approved_fixed_limits()
    {
        Assert.True(new AutoDevCatalogSourceOptions().IsValidForWorker());
        Assert.False(new AutoDevCatalogSourceOptions { PageSize = 21 }.IsValidForWorker());
    }

    [Fact]
    public async Task Reads_the_fixed_page_shape_with_bearer_and_maps_nested_values()
    {
        HttpRequestMessage? captured = null;
        var handler = new DelegateHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"data":[{"vehicle":{"vin":"vehicle-vin","year":2024,"make":"Jeep","model":"Grand Cherokee","trim":"4xe","miles":12,"doors":4},"retailListing":{"price":100,"miles":13,"city":"A","state":"SP","vdp":"listing","primaryImage":"image"}}],"links":{"next":"next"}}""") };
        });
        var source = new AutoDevCatalogSource(new HttpClient(handler) { BaseAddress = new Uri("https://auto.dev/") },
            Options.Create(new AutoDevOptions { BaseUrl = "https://auto.dev/", ApiKey = "secret" }), new AutoDevCatalogSourceOptions(), new Pacer(), TimeProvider.System);
        var page = await source.ReadPageAsync(1, 999, TestContext.Current.CancellationToken);
        Assert.Equal("?page=1&limit=20&sort=updatedAt.desc&includeUnpriced=false", captured!.RequestUri!.Query);
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme); Assert.Equal("secret", captured.Headers.Authorization.Parameter);
        var item = Assert.Single(page.Items);
        Assert.Equal("vehicle-vin", item.ExternalId); Assert.Equal(12, item.Mileage); Assert.True(page.HasNext);
    }

    [Fact]
    public async Task Numeric_vehicle_model_is_preserved_as_text()
    {
        var source = CreateSource(new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"data":[{"vehicle":{"vin":"vin-3","model":3}}],"links":{}}""")
        }));

        var page = await source.ReadPageAsync(1, 20, TestContext.Current.CancellationToken);

        Assert.Equal("3", Assert.Single(page.Items).Model);
    }

    [Fact]
    public async Task Numeric_vehicle_text_attributes_are_preserved_as_raw_invariant_text()
    {
        var source = CreateSource(new DelegateHandler(_ => JsonPage("""{"data":[{"vehicle":{"vin":"vin-flex","model":3,"trim":-2.5,"fuel":1e+3,"transmission":4,"exteriorColor":-5.25}}],"links":{}}""")));

        var item = Assert.Single((await source.ReadPageAsync(1, 20, TestContext.Current.CancellationToken)).Items);

        Assert.Equal("3", item.Model);
        Assert.Equal("-2.5", item.Version);
        Assert.Equal("1e+3", item.Fuel);
        Assert.Equal("4", item.Transmission);
        Assert.Equal("-5.25", item.Color);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task Flexible_vehicle_text_attributes_reject_non_string_number_or_null(string invalidJson)
    {
        var source = CreateSource(new DelegateHandler(_ => JsonPage("{\"data\":[{\"vehicle\":{\"vin\":\"vin-invalid\",\"trim\":" + invalidJson + "}}],\"links\":{}}")));

        await Assert.ThrowsAsync<JsonException>(() => source.ReadPageAsync(1, 20, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("make")]
    [InlineData("vin")]
    public async Task Strict_vehicle_string_fields_still_reject_numbers(string property)
    {
        var source = CreateSource(new DelegateHandler(_ => JsonPage("{\"data\":[{\"vehicle\":{\"" + property + "\":3}}],\"links\":{}}")));

        await Assert.ThrowsAsync<JsonException>(() => source.ReadPageAsync(1, 20, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("city")]
    [InlineData("vdp")]
    [InlineData("primaryImage")]
    public async Task Strict_retail_listing_string_fields_still_reject_numbers(string property)
    {
        var source = CreateSource(new DelegateHandler(_ => JsonPage("{\"data\":[{\"retailListing\":{\"" + property + "\":3}}],\"links\":{}}")));

        await Assert.ThrowsAsync<JsonException>(() => source.ReadPageAsync(1, 20, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Page_over_fifty_is_empty_without_http()
    {
        var handler = new DelegateHandler(_ => throw new Xunit.Sdk.XunitException("HTTP must not be called"));
        var source = new AutoDevCatalogSource(new HttpClient(handler), Options.Create(new AutoDevOptions { ApiKey = "secret" }), new AutoDevCatalogSourceOptions(), new Pacer(), TimeProvider.System);
        var page = await source.ReadPageAsync(51, 20, TestContext.Current.CancellationToken);
        Assert.Empty(page.Items); Assert.False(page.HasNext);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Transient_statuses_retry_before_a_success(int status)
    {
        var calls = 0;
        var source = CreateSource(new DelegateHandler(_ => ++calls <= 3
            ? new HttpResponseMessage((HttpStatusCode)status)
            : JsonPage()));
        await source.ReadPageAsync(1, 20, TestContext.Current.CancellationToken);
        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task Non_transient_client_error_is_not_retried()
    {
        var calls = 0;
        var source = CreateSource(new DelegateHandler(_ => { calls++; return new HttpResponseMessage(HttpStatusCode.BadRequest); }));
        await Assert.ThrowsAsync<HttpRequestException>(() => source.ReadPageAsync(1, 20, TestContext.Current.CancellationToken));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Attempt_sixty_one_is_rejected_before_http()
    {
        var calls = 0;
        var source = CreateSource(new DelegateHandler(_ => { calls++; return new HttpResponseMessage(HttpStatusCode.InternalServerError); }));
        for (var page = 1; page <= 15; page++)
            await Assert.ThrowsAsync<HttpRequestException>(() => source.ReadPageAsync(page, 20, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.ReadPageAsync(16, 20, TestContext.Current.CancellationToken));
        Assert.Equal(60, calls);
    }

    [Fact]
    public async Task Fifty_successful_pages_yield_one_thousand_items_and_never_request_page_fifty_one()
    {
        var sends = 0;
        var source = CreateSource(new DelegateHandler(_ =>
        {
            sends++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PageJson(sends, sends <= 50)) };
        }));
        var total = 0;
        CatalogSourcePage? final = null;
        for (var page = 1; page <= 50; page++) { final = await source.ReadPageAsync(page, 20, TestContext.Current.CancellationToken); total += final!.Items.Count; }
        var beyond = await source.ReadPageAsync(51, 20, TestContext.Current.CancellationToken);
        Assert.Equal(1_000, total); Assert.False(final!.HasNext); Assert.Empty(beyond.Items); Assert.Equal(50, sends);
    }

    [Fact]
    public async Task Internal_timeout_retries_each_attempt_while_caller_cancellation_stops_immediately()
    {
        var sends = 0; var pacer = new CountingPacer();
        var source = new AutoDevCatalogSource(new HttpClient(new BlockingHandler(() => sends++)) { BaseAddress = new Uri("https://auto.dev/") },
            Options.Create(new AutoDevOptions { ApiKey = "secret" }), new AutoDevCatalogSourceOptions { RequestTimeout = TimeSpan.FromMilliseconds(20) }, pacer, TimeProvider.System);
        await Assert.ThrowsAsync<HttpRequestException>(() => source.ReadPageAsync(1, 20, TestContext.Current.CancellationToken));
        Assert.Equal(4, sends); Assert.Equal(4, pacer.Calls);

        var callerSends = 0; var callerPacer = new CountingPacer();
        var callerSource = new AutoDevCatalogSource(new HttpClient(new BlockingHandler(() => callerSends++)) { BaseAddress = new Uri("https://auto.dev/") },
            Options.Create(new AutoDevOptions { ApiKey = "secret" }), new AutoDevCatalogSourceOptions(), callerPacer, TimeProvider.System);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callerSource.ReadPageAsync(1, 20, cancelled.Token));
        Assert.Equal(1, callerSends); Assert.Equal(1, callerPacer.Calls);
    }

    [Fact]
    public async Task Repeated_accepted_page_retries_until_different_content_is_returned()
    {
        var responses = new Queue<string>([VehiclePage("vin-a", "A"), VehiclePage("vin-a", "A"), VehiclePage("vin-b", "B")]);
        var sends = 0;
        var source = CreateSource(new DelegateHandler(_ => { sends++; return JsonPage(responses.Dequeue()); }));
        _ = await source.ReadPageAsync(1, 20, TestContext.Current.CancellationToken);

        var second = await source.ReadPageAsync(2, 20, TestContext.Current.CancellationToken);

        Assert.Equal("vin-b", Assert.Single(second.Items).ExternalId);
        Assert.Equal(3, sends);
    }

    [Fact]
    public async Task Repeated_page_after_all_retries_returns_terminal_empty_page()
    {
        var sends = 0;
        var source = CreateSource(new DelegateHandler(_ => { sends++; return JsonPage(VehiclePage("vin-a", "A")); }));
        _ = await source.ReadPageAsync(1, 20, TestContext.Current.CancellationToken);

        var terminal = await source.ReadPageAsync(2, 20, TestContext.Current.CancellationToken);

        Assert.Empty(terminal.Items);
        Assert.False(terminal.HasNext);
        Assert.Equal(5, sends);
    }

    [Fact]
    public async Task Same_vin_with_different_mapped_content_is_accepted_without_retry()
    {
        var responses = new Queue<string>([VehiclePage("same-vin", "A"), VehiclePage("same-vin", "B")]);
        var sends = 0;
        var source = CreateSource(new DelegateHandler(_ => { sends++; return JsonPage(responses.Dequeue()); }));
        _ = await source.ReadPageAsync(1, 20, TestContext.Current.CancellationToken);

        var second = await source.ReadPageAsync(2, 20, TestContext.Current.CancellationToken);

        Assert.Equal("B", Assert.Single(second.Items).Model);
        Assert.Equal(2, sends);
    }

    private static AutoDevCatalogSource CreateSource(HttpMessageHandler handler) => new(new HttpClient(handler) { BaseAddress = new Uri("https://auto.dev/") },
        Options.Create(new AutoDevOptions { BaseUrl = "https://auto.dev/", ApiKey = "secret" }), new AutoDevCatalogSourceOptions(), new Pacer(), TimeProvider.System);
    private static HttpResponseMessage JsonPage() => new(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[],\"links\":{}}") };
    private static HttpResponseMessage JsonPage(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
    private static string VehiclePage(string vin, string model) =>
        "{\"data\":[{\"vehicle\":{\"vin\":\"" + vin + "\",\"model\":\"" + model + "\"}}],\"links\":{\"next\":\"next\"}}";
    private static string PageJson(int page, bool next) => "{\"data\":[" + string.Join(',', Enumerable.Range(1, 20).Select(value => "{\"vin\":\"v" + page + "-" + value + "\"}")) + "],\"links\":{" + (next ? "\"next\":\"next\"" : string.Empty) + "}}";

    private sealed class Pacer : IAutoDevRequestPacer { public Task WaitAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class CountingPacer : IAutoDevRequestPacer { public int Calls { get; private set; } public Task WaitAsync(CancellationToken cancellationToken) { Calls++; return Task.CompletedTask; } }
    private sealed class BlockingHandler(Func<int> sent) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { sent(); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return new HttpResponseMessage(HttpStatusCode.OK); }
    }
    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(handle(request));
    }
}
