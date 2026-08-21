using System.Net;
using System.Text;
using System.Net.Http.Headers;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Services;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;

namespace Buscarro.Tests;

public sealed class CarzinCatalogSourceTests
{
    [Fact]
    public void Classified_contract_models_provider_codes_only_for_hierarchical_taxonomy()
    {
        var properties = typeof(Buscarro.Infrastructure.Contracts.CarzinClassifiedContract).GetProperties();

        Assert.DoesNotContain(properties, property => property.Name is "Manufacturer" or "Model" or "Version");
        Assert.Contains(properties, property => property.Name == "ManufacturerId");
        Assert.Contains(properties, property => property.Name == "ModelId");
        Assert.Contains(properties, property => property.Name == "VersionId");
        Assert.Contains(properties, property => property.Name == "Color");
        Assert.Contains(properties, property => property.Name == "Gear");
        Assert.Contains(properties, property => property.Name == "Fuel");
        Assert.DoesNotContain(properties, property => property.Name is "ColorCode" or "GearCode" or "FuelCode");
    }

    [Fact]
    public async Task Attribute_names_from_real_payload_are_trimmed_and_mapped_directly()
    {
        const string json = """{"results":[{"id":7,"title":"Civic","manufacturer_id":1,"model_id":2,"color":" Prata ","gear":" Manual ","fuel":" Gasolina e Álcool "}]}""";
        var source = CreateSource(new RecordingHandler(_ => Task.FromResult(JsonResponse(json))),
            new CarzinCatalogSourceOptions { RetryCount = 0 }, new NoRetryDelay());

        var item = Assert.Single((await source.ReadPageAsync(1, 100, TestContext.Current.CancellationToken)).Items);

        Assert.Equal("Prata", item.Color);
        Assert.Equal("Manual", item.Transmission);
        Assert.Equal("Gasolina e Álcool", item.Fuel);
    }

    [Theory]
    [InlineData("1.9")]
    [InlineData("2147483648")]
    public async Task Fractional_or_out_of_range_classified_ids_are_rejected_instead_of_truncated(string rawId)
    {
        var json = $$"""{"results":[{"id":7,"title":"Civic","manufacturer_id":{{rawId}},"model_id":"1.9","version_id":2147483648}]}""";
        var source = CreateSource(new RecordingHandler(_ => Task.FromResult(JsonResponse(json))),
            new CarzinCatalogSourceOptions { RetryCount = 0 }, new NoRetryDelay());

        var item = Assert.Single((await source.ReadPageAsync(1, 100, TestContext.Current.CancellationToken)).Items);

        Assert.Null(item.ManufacturerCode);
        Assert.Null(item.ModelCode);
        Assert.Null(item.VersionCode);
    }

    [Fact]
    public async Task Sends_only_proven_search_fields_and_maps_provider_payload()
    {
        string? body = null;
        var handler = new RecordingHandler(async request =>
        {
            body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"current_page":2,"total_pages":2,"total_results":1,"results":[{"id":"7","title":" Civic ","manufacturer_id":1,"model_id":2,"year_model":2024,"city":"S&atilde;o Paulo"}]}""", Encoding.UTF8, "application/json")
            };
        });
        var options = new CarzinCatalogSourceOptions { RetryCount = 0 };
        var source = new CarzinCatalogSource(new HttpClient(handler) { BaseAddress = new Uri("https://carzin.test") },
            Options.Create(new CarzinOptions { BaseUrl = "https://carzin.test", Token = "secret" }), options,
            new CarzinCircuitBreaker(TimeProvider.System, options), TimeProvider.System, new NoRetryDelay());

        var page = await source.ReadPageAsync(2, 100, TestContext.Current.CancellationToken);

        Assert.Contains("name=token", body); Assert.Contains("name=type", body); Assert.Contains("name=page", body); Assert.Contains("name=results", body);
        Assert.DoesNotContain("manufacturer", body); Assert.DoesNotContain("model", body); Assert.DoesNotContain("version", body);
        var vehicle = Assert.Single(page.Items);
        Assert.Equal("7", vehicle.ExternalId); Assert.Equal("1", vehicle.ManufacturerCode); Assert.Equal("São Paulo", vehicle.City);
        Assert.Null(page.CurrentResults);
        Assert.Null(vehicle.Mileage);
        Assert.Null(vehicle.Price);
    }

    [Fact]
    public void Advisory_lock_key_is_stable_and_provider_normalized()
    {
        Assert.Equal(PostgresProviderSynchronizationLock.ProviderKey(" carzin "), PostgresProviderSynchronizationLock.ProviderKey("CARZIN"));
    }

    [Fact]
    public async Task Non_positive_provider_years_map_to_absence_instead_of_invented_zero()
    {
        var options = new CarzinCatalogSourceOptions { RetryCount = 0 };
        var source = CreateSource(new RecordingHandler(_ => Task.FromResult(JsonResponse(
            """{"results":[{"id":1,"title":"Carro","year_model":0,"year_manufacture":0}]}"""))), options, new NoRetryDelay());

        var page = await source.ReadPageAsync(1, 100, TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(page.Items).Year);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Carzin_non_positive_numeric_id_maps_to_missing_for_generic_validation(int id)
    {
        var options = new CarzinCatalogSourceOptions { RetryCount = 0 };
        var source = CreateSource(new RecordingHandler(_ => Task.FromResult(JsonResponse(
            $$"""{"results":[{"id":{{id}},"title":"Carro"}]}"""))), options, new NoRetryDelay());

        var page = await source.ReadPageAsync(1, 100, TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(page.Items).ExternalId);
    }

    [Fact]
    public async Task Transient_failures_execute_initial_attempt_plus_exactly_three_retries()
    {
        var metrics=new ConcurrentBag<(string Name,long Value)>();var durations=new ConcurrentBag<string>();using var listener=new MeterListener();
        listener.InstrumentPublished=(instrument,l)=>{if(instrument.Meter.Name=="Buscarro.Catalog")l.EnableMeasurementEvents(instrument);};
        listener.SetMeasurementEventCallback<long>((instrument,value,_,_)=>metrics.Add((instrument.Name,value)));
        listener.SetMeasurementEventCallback<double>((instrument,_,_,_)=>durations.Add(instrument.Name));listener.Start();
        var attempts = 0;
        var delay = new RecordingRetryDelay();
        var source = CreateSource(new RecordingHandler(_ =>
        {
            attempts++;
            return Task.FromResult(attempts < 4
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : JsonResponse("""{"current_results":0,"current_page":1,"total_pages":1,"total_results":0,"results":[]}"""));
        }), new CarzinCatalogSourceOptions(), delay);

        var page = await source.ReadPageAsync(1, 100, TestContext.Current.CancellationToken);

        Assert.Empty(page.Items);
        Assert.Equal(4, attempts);
        Assert.Equal([0, 1, 2], delay.Attempts);
        Assert.Equal(3,metrics.Count(x=>x.Name=="catalog.sync.retries"&&x.Value==1));
        Assert.Equal(1,durations.Count(x=>x=="catalog.sync.provider_request.duration"));
    }

    [Fact]
    public async Task Caller_cancellation_stops_before_any_retry()
    {
        var timeouts=new ConcurrentBag<long>();using var listener=TimeoutListener(timeouts);
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        var delay = new RecordingRetryDelay();
        var source = CreateSource(new RecordingHandler(_ =>
        {
            attempts++; cancellation.Cancel();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }), new CarzinCatalogSourceOptions(), delay);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.ReadPageAsync(1, 100, cancellation.Token));

        Assert.Equal(1, attempts);
        Assert.Empty(delay.Attempts);
        Assert.Empty(timeouts);
    }

    [Fact]
    public async Task Internal_request_timeout_is_retried_three_times_then_wrapped()
    {
        var timeouts=new ConcurrentBag<long>();using var listener=TimeoutListener(timeouts);
        var attempts = 0;
        var delay = new RecordingRetryDelay();
        var source = CreateSource(new RecordingHandler(_ =>
        {
            attempts++;
            return Task.FromCanceled<HttpResponseMessage>(new CancellationToken(canceled: true));
        }), new CarzinCatalogSourceOptions(), delay);

        await Assert.ThrowsAsync<HttpRequestException>(() => source.ReadPageAsync(1, 100, TestContext.Current.CancellationToken));

        Assert.Equal(4, attempts);
        Assert.Equal([0, 1, 2], delay.Attempts);
        Assert.Equal(4,timeouts.Sum());
    }

    [Fact]
    public async Task Http_408_records_timeout_for_each_attempt_including_final_without_double_counting()
    {
        var timeouts=new ConcurrentBag<long>();using var listener=TimeoutListener(timeouts);var attempts=0;var delay=new RecordingRetryDelay();
        var source=CreateSource(new RecordingHandler(_=>{attempts++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestTimeout));}),new CarzinCatalogSourceOptions(),delay);
        await Assert.ThrowsAsync<HttpRequestException>(()=>source.ReadPageAsync(1,100,CancellationToken.None));
        Assert.Equal(4,attempts);Assert.Equal(4,timeouts.Sum());Assert.Equal([0,1,2],delay.Attempts);
    }

    [Fact]
    public async Task Retry_after_is_forwarded_to_deterministic_delay_policy()
    {
        var attempts = 0;
        var delay = new RecordingRetryDelay();
        var source = CreateSource(new RecordingHandler(_ =>
        {
            attempts++;
            if (attempts > 1) return Task.FromResult(JsonResponse("""{"current_results":0,"results":[]}"""));
            var response = new HttpResponseMessage((HttpStatusCode)429);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(9));
            return Task.FromResult(response);
        }), new CarzinCatalogSourceOptions(), delay);

        await source.ReadPageAsync(1, 100, TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromSeconds(9), Assert.Single(delay.RetryAfter)!.Delta);
    }

    [Fact]
    public void Retry_delay_combines_exponential_jitter_and_retry_after_deterministically()
    {
        var policy = new CarzinRetryDelay(
            new CarzinCatalogSourceOptions { RetryBaseDelay = TimeSpan.FromSeconds(1), RetryMaxDelay = TimeSpan.FromSeconds(30) },
            TimeProvider.System,
            () => 125);

        Assert.Equal(TimeSpan.FromMilliseconds(4_125), policy.CalculateDelay(2, null));
        Assert.Equal(TimeSpan.FromSeconds(10), policy.CalculateDelay(2, new RetryConditionHeaderValue(TimeSpan.FromSeconds(10))));
    }

    [Fact]
    public void Circuit_breaker_opens_allows_single_half_open_probe_and_closes_on_success()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero));
        var options = new CarzinCatalogSourceOptions { CircuitFailureThreshold = 2, CircuitBreakDuration = TimeSpan.FromMinutes(1) };
        var breaker = new CarzinCircuitBreaker(clock, options);
        using (breaker.Enter()) breaker.Failure();
        using (breaker.Enter()) breaker.Failure();
        Assert.Throws<InvalidOperationException>(() => breaker.Enter());

        clock.Advance(TimeSpan.FromMinutes(1));
        using var probe = breaker.Enter();
        Assert.Throws<InvalidOperationException>(() => breaker.Enter());
        breaker.Success();
        probe.Dispose();
        using var closed = breaker.Enter();
    }

    private static CarzinCatalogSource CreateSource(HttpMessageHandler handler, CarzinCatalogSourceOptions options, ICarzinRetryDelay delay)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://carzin.test") },
            Options.Create(new CarzinOptions { BaseUrl = "https://carzin.test", Token = "secret" }), options,
            new CarzinCircuitBreaker(TimeProvider.System, options), TimeProvider.System, delay);

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request);
    }

    private static MeterListener TimeoutListener(ConcurrentBag<long> values)
    {
        var listener=new MeterListener();listener.InstrumentPublished=(instrument,l)=>{if(instrument.Name=="catalog.sync.timeouts")l.EnableMeasurementEvents(instrument);};
        listener.SetMeasurementEventCallback<long>((_,value,_,_)=>values.Add(value));listener.Start();return listener;
    }

    private sealed class NoRetryDelay : ICarzinRetryDelay
    {
        public Task DelayAsync(int attempt, System.Net.Http.Headers.RetryConditionHeaderValue? retryAfter, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class RecordingRetryDelay : ICarzinRetryDelay
    {
        public List<int> Attempts { get; } = [];
        public List<RetryConditionHeaderValue?> RetryAfter { get; } = [];
        public Task DelayAsync(int attempt, RetryConditionHeaderValue? retryAfter, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts.Add(attempt); RetryAfter.Add(retryAfter); return Task.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
