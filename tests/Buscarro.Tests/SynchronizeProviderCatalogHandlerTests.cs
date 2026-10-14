using System.Reflection;
using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using Buscarro.Domain.Enums;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;

namespace Buscarro.Tests;

public sealed class SynchronizeProviderCatalogHandlerTests
{
    [Fact]
    public async Task Lock_contention_skips_without_creating_generation_or_reading_source()
    {
        var fixture = new Fixture(acquire: false);
        var result = await fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);
        Assert.False(result.Acquired);
        Assert.Empty(fixture.Generations.Values);
        Assert.Equal(0, fixture.Source.Reads);
        Assert.Equal(0, fixture.Normalizer.Prepares);
    }

    [Fact]
    public async Task Acquired_synchronization_prepares_once_before_paging_and_normalizes_every_item()
    {
        var fixture = new Fixture();
        fixture.Source.Pages.Enqueue(new(1, 1, 2, 1, 2, [Vehicle("1", "A"), Vehicle("2", "B")]));

        await fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);

        Assert.Equal(1, fixture.Normalizer.Prepares);
        Assert.Equal(2, fixture.Normalizer.Normalizations);
        Assert.True(fixture.Source.PreparedWhenRead);
    }

    [Fact]
    public async Task Taxonomy_preparation_failure_happens_before_generation_and_provider_page()
    {
        var fixture = new Fixture();
        fixture.Normalizer.PrepareException = new InvalidOperationException("carzin_taxonomy_incomplete");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));

        Assert.Equal("carzin_taxonomy_incomplete", exception.Message);
        Assert.Empty(fixture.Generations.Values);
        Assert.Equal(0, fixture.Source.Reads);
        Assert.Equal(0, fixture.FailureRecorder.Calls);
    }

    [Fact]
    public async Task Normalization_failure_counts_invalid_without_writing_item_and_keeps_publication_gates()
    {
        var fixture = new Fixture();
        fixture.Normalizer.NormalizeResult = source => source.ExternalId == "2"
            ? CatalogItemNormalizationResult.Invalid("color_code_unknown")
            : CatalogItemNormalizationResult.Valid(source);
        fixture.Source.Pages.Enqueue(new(1, 1, 2, 1, 2, [Vehicle("1", "A"), Vehicle("2", "B")]));

        var result = await fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ValidItemCount);
        Assert.Equal(1, result.InvalidItemCount);
        Assert.Single(fixture.Writer.Items);
        var generation = Assert.Single(fixture.Generations.Values);
        Assert.Equal(CatalogGenerationStatus.Current, generation.Status);
        Assert.Contains("color_code_unknown", generation.ValidationSummaryJson);
    }

    [Fact]
    public async Task Zero_valid_items_rejects_first_publication_and_preserves_aggregate_reason_summary()
    {
        var fixture = new Fixture();
        fixture.Source.Pages.Enqueue(new(1, 1, 1, 1, 1, [Vehicle(null, "A")]));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));

        var generation = Assert.Single(fixture.Generations.Values);
        Assert.Contains("missing_external_id", generation.ValidationSummaryJson);
        Assert.Equal(1, fixture.FailureRecorder.Calls);
        Assert.Empty(fixture.Writer.Items);
    }

    [Fact]
    public async Task Synchronization_counts_duplicates_writes_only_unique_items_and_publishes_ready()
    {
        var fixture = new Fixture();
        fixture.Source.Pages.Enqueue(new(1, 1, 3, 1, 3,
        [
            Vehicle("1", "Honda Civic"), Vehicle("1", "Duplicado"), Vehicle("2", "Toyota Corolla")
        ]));

        var result = await fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);

        Assert.Equal(3, result.SourceItemCount);
        Assert.Equal(2, result.ValidItemCount);
        Assert.Equal(1, result.DuplicateItemCount);
        Assert.Equal(1,result.PageCount);
        Assert.Equal(2, fixture.Writer.Items.Count);
        Assert.Equal(CatalogGenerationStatus.Current, fixture.Generations.Values.Single().Status);
    }

    [Fact]
    public async Task Synchronization_metrics_report_actual_page_and_item_counts_and_single_aggregate_durations()
    {
        var values=new ConcurrentBag<(string Name,long Value)>();var durationCounts=new ConcurrentDictionary<string,int>();using var listener=new MeterListener();
        listener.InstrumentPublished=(instrument,l)=>{if(instrument.Meter.Name=="Buscarro.Catalog")l.EnableMeasurementEvents(instrument);};
        listener.SetMeasurementEventCallback<long>((instrument,value,_,_)=>values.Add((instrument.Name,value)));
        listener.SetMeasurementEventCallback<double>((instrument,value,_,_)=>durationCounts.AddOrUpdate(instrument.Name,1,(_,count)=>count+1));listener.Start();
        var fixture=new Fixture();fixture.Source.Pages.Enqueue(new(1,1,2,1,2,[Vehicle("1","A"),Vehicle("2","B")]));
        await fixture.Handler.Handle(new("Carzin"),CancellationToken.None);
        Assert.Contains(values,x=>x.Name=="catalog.sync.pages"&&x.Value==1);Assert.Contains(values,x=>x.Name=="catalog.sync.items.source"&&x.Value==2);
        Assert.Equal(1,durationCounts["catalog.sync.validation.duration"]);Assert.Equal(1,durationCounts["catalog.sync.write.duration"]);
    }

    [Fact]
    public async Task Publisher_failure_leaves_generation_ready_and_recorder_observes_persisted_ready()
    {
        var fixture = new Fixture(publishFailure: true);
        fixture.Source.Pages.Enqueue(new(1, 1, 1, 1, 1, [Vehicle("1", "Honda Civic")]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));

        Assert.Equal(CatalogGenerationStatus.Ready, fixture.Generations.Values.Single().Status);
        Assert.Equal(1, fixture.FailureRecorder.Calls);
    }

    [Fact]
    public async Task Snapshot_metadata_failure_records_loading_generation_as_failed()
    {
        var fixture = new Fixture(pageSize: 1);
        fixture.Source.Pages.Enqueue(new(1, 1, 1, 1, 2, [Vehicle(null, "Honda Civic")]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));

        Assert.Equal(1, fixture.FailureRecorder.Calls);
    }

    [Fact]
    public async Task Same_ids_with_changed_content_are_not_a_false_repeated_page()
    {
        var fixture = new Fixture(pageSize: 1);
        fixture.Source.Pages.Enqueue(new(1, 1, 1, 2, 2, [Vehicle("1", "Honda Civic")]));
        fixture.Source.Pages.Enqueue(new(2, 2, 1, 2, 2, [Vehicle("1", "Honda Civic alterado")]));

        var result = await fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ValidItemCount);
        Assert.Equal(1, result.DuplicateItemCount);
        Assert.Equal(0, fixture.FailureRecorder.Calls);
    }

    [Fact]
    public async Task Identical_full_page_fingerprint_is_rejected()
    {
        var fixture = new Fixture(pageSize: 1);
        fixture.Source.Pages.Enqueue(new(1, 1, 1, 2, 2, [Vehicle("1", "Honda Civic")]));
        fixture.Source.Pages.Enqueue(new(2, 2, 1, 2, 2, [Vehicle("1", "Honda Civic")]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));

        Assert.Equal(1, fixture.FailureRecorder.Calls);
    }

    [Fact]
    public async Task Page_larger_than_requested_limit_is_rejected_before_writes()
    {
        var fixture = new Fixture();
        fixture.Source.Pages.Enqueue(new(1, 1, 101, 1, 101,
            Enumerable.Range(1, 101).Select(value => Vehicle(value.ToString(), "Carro")).ToArray()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));

        Assert.Empty(fixture.Writer.Items);
        Assert.Equal(1, fixture.FailureRecorder.Calls);
    }

    [Fact]
    public async Task External_cancellation_during_preparation_does_not_create_generation_or_record_failure()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        fixture.Source.Exception = new OperationCanceledException(cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Handler.Handle(new("Carzin"), cancellation.Token));

        Assert.Empty(fixture.Generations.Values);
        Assert.Equal(0, fixture.FailureRecorder.Calls);
    }

    [Fact]
    public async Task Missing_numeric_values_remain_null_in_persisted_item()
    {
        var fixture = new Fixture();
        var source = Vehicle("1", "Sem dados") with { Year = null, Mileage = null, Price = null };
        fixture.Source.Pages.Enqueue(new(1, 1, 1, 1, 1, [source]));

        await fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);

        var item = Assert.Single(fixture.Writer.Items);
        Assert.Null(item.Year); Assert.Null(item.Mileage); Assert.Null(item.Price);
    }

    [Fact]
    public async Task Provider_taxonomy_codes_remain_transient_and_are_not_copied_to_catalog_item()
    {
        var fixture = new Fixture();
        fixture.Source.Pages.Enqueue(new(1, 1, 1, 1, 1,
            [Vehicle("1", "Civic") with { ManufacturerCode = "1", ModelCode = "2", VersionCode = "3" }]));

        await fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);

        var item = Assert.Single(fixture.Writer.Items);
        Assert.Null(item.GetType().GetProperty("ManufacturerCode"));
        Assert.Null(item.GetType().GetProperty("ModelCode"));
        Assert.Null(item.GetType().GetProperty("VersionCode"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Total_metadata_presence_cannot_change_after_first_page(bool appears)
    {
        var fixture = new Fixture(pageSize: 1);
        fixture.Source.Pages.Enqueue(appears
            ? new(1, 1, 1, null, null, [Vehicle("1", "A")])
            : new(1, 1, 1, 2, 2, [Vehicle("1", "A")]));
        fixture.Source.Pages.Enqueue(appears
            ? new(2, 2, 1, 2, 2, [Vehicle("2", "B")])
            : new(2, 2, 1, null, null, [Vehicle("2", "B")]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Short_page_without_totals_terminates_immediately()
    {
        var fixture = new Fixture();
        fixture.Source.Pages.Enqueue(new(1, null, 1, null, null, [Vehicle("1", "A")]));

        await fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);

        Assert.Equal(1, fixture.Source.Reads);
    }

    [Fact]
    public async Task Has_next_false_cannot_mask_known_remaining_total_pages()
    {
        var fixture = new Fixture(pageSize: 1);
        fixture.Source.Pages.Enqueue(new(1, 1, 1, 2, null, [Vehicle("1", "A")], false));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));

        Assert.Equal("catalog_has_next_missing_before_final", exception.Message);
        Assert.Equal(1, fixture.FailureRecorder.Calls);
    }

    [Fact]
    public async Task Empty_page_terminates_sequence_after_a_full_page()
    {
        var fixture = new Fixture(pageSize: 1);
        fixture.Source.Pages.Enqueue(new(1, null, 1, null, null, [Vehicle("1", "A")]));
        fixture.Source.Pages.Enqueue(new(2, null, 0, null, null, []));

        await fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);

        Assert.Equal(2, fixture.Source.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_total_pages_rejects_empty_or_short_page_before_final(bool empty)
    {
        var fixture = new Fixture(pageSize: 2);
        CatalogSourceVehicle[] items = empty ? [] : [Vehicle("1", "A")];
        fixture.Source.Pages.Enqueue(new(1, 1, items.Length, 2, items.Length, items));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));

        Assert.Equal(1, fixture.FailureRecorder.Calls);
    }

    [Fact]
    public async Task Current_results_must_equal_materialized_page_count()
    {
        var fixture = new Fixture();
        fixture.Source.Pages.Enqueue(new(1, 1, 2, 1, 1, [Vehicle("1", "A")]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));
        Assert.Empty(fixture.Writer.Items);
    }

    [Fact]
    public async Task Total_metadata_value_cannot_change()
    {
        var fixture = new Fixture(pageSize: 1);
        fixture.Source.Pages.Enqueue(new(1, 1, 1, 2, 2, [Vehicle("1", "A")]));
        fixture.Source.Pages.Enqueue(new(2, 2, 1, 2, 3, [Vehicle("2", "B")]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Total_pages_value_cannot_change()
    {
        var fixture = new Fixture(pageSize: 1);
        fixture.Source.Pages.Enqueue(new(1, 1, 1, 3, 2, [Vehicle("1", "A")]));
        fixture.Source.Pages.Enqueue(new(2, 2, 1, 2, 2, [Vehicle("2", "B")]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reported_total_pages_cannot_be_before_requested_page()
    {
        var fixture = new Fixture(pageSize: 1);
        fixture.Source.Pages.Enqueue(new(1, 1, 1, null, null, [Vehicle("1", "A")]));
        fixture.Source.Pages.Enqueue(new(2, 2, 1, 1, null, [Vehicle("2", "B")]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Configured_page_and_vehicle_limits_are_enforced_before_writes()
    {
        var pageLimited = new Fixture(maxPages: 1);
        pageLimited.Source.Pages.Enqueue(new(1, 1, 1, 2, 2, [Vehicle("1", "A")]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pageLimited.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));

        var vehicleLimited = new Fixture(maxVehicles: 1);
        vehicleLimited.Source.Pages.Enqueue(new(1, 1, 2, 1, 2, [Vehicle("1", "A"), Vehicle("2", "B")]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => vehicleLimited.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));

        Assert.Empty(pageLimited.Writer.Items);
        Assert.Empty(vehicleLimited.Writer.Items);
    }

    [Fact]
    public async Task Checksum_is_deterministic_for_identical_canonical_items_including_null_markers()
    {
        var first = new Fixture();
        var second = new Fixture();
        var vehicle = Vehicle("1", "A") with { Year = null, Mileage = null, Price = null };
        first.Source.Pages.Enqueue(new(1, 1, 1, 1, 1, [vehicle]));
        second.Source.Pages.Enqueue(new(1, 1, 1, 1, 1, [vehicle]));

        await first.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);
        await second.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);

        Assert.Equal(first.Generations.Values.Single().ChecksumSha256, second.Generations.Values.Single().ChecksumSha256);
    }

    [Fact]
    public async Task Reordered_same_content_is_not_claimed_as_identical_page_repetition()
    {
        var fixture = new Fixture(pageSize: 2);
        var a = Vehicle("1", "A"); var b = Vehicle("2", "B");
        fixture.Source.Pages.Enqueue(new(1, 1, 2, 2, 4, [a, b]));
        fixture.Source.Pages.Enqueue(new(2, 2, 2, 2, 4, [b, a]));

        var result = await fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ValidItemCount);
        Assert.Equal(2, result.DuplicateItemCount);
    }

    [Fact]
    public async Task Pages_that_differ_only_by_attribute_name_do_not_share_fingerprint()
    {
        var fixture = new Fixture(pageSize: 1);
        var vehicle = Vehicle("1", "A") with { Color = "Prata" };
        fixture.Source.Pages.Enqueue(new(1, 1, 1, 2, 2, [vehicle]));
        fixture.Source.Pages.Enqueue(new(2, 2, 1, 2, 2, [vehicle with { Color = "Preto" }]));

        var result = await fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ValidItemCount);
        Assert.Equal(1, result.DuplicateItemCount);
    }

    [Fact]
    public async Task Internal_timeout_uses_independent_failure_recorder()
    {
        var fixture = new Fixture(timeout: TimeSpan.FromMilliseconds(20));
        fixture.Source.BlockUntilCanceled = true;

        await Assert.ThrowsAsync<TimeoutException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));

        Assert.Equal(1, fixture.FailureRecorder.Calls);
    }

    [Fact]
    public async Task Save_failure_after_mark_ready_still_invokes_independent_failure_recorder()
    {
        var fixture = new Fixture(failOnSaveCall: 2);
        fixture.Source.Pages.Enqueue(new(1, 1, 1, 1, 1, [Vehicle("1", "A")]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(new("Carzin"), TestContext.Current.CancellationToken));

        Assert.Equal(CatalogGenerationStatus.Ready, fixture.Generations.Values.Single().Status);
        Assert.Equal(1, fixture.FailureRecorder.Calls);
    }

    private static CatalogSourceVehicle Vehicle(string? id, string title) => new(id, title, "Marca", "Modelo", null,
        null, null, null, 2024, 1, null, null, null, null, null, 10, null, null, null, null);

    private sealed class Fixture
    {
        public Source Source { get; } = new();
        public GenerationRepository Generations { get; } = new();
        public Writer Writer { get; } = new();
        public FailureRecorder FailureRecorder { get; } = new();
        public Normalizer Normalizer { get; } = new();
        public SynchronizeProviderCatalogHandler Handler { get; }

        public Fixture(
            bool acquire = true,
            bool publishFailure = false,
            int pageSize = 100,
            int maxPages = 50_000,
            int maxVehicles = 2_000_000,
            TimeSpan? timeout = null,
            int? failOnSaveCall = null)
        {
            var options = new CatalogSynchronizationOptions
            {
                PageSize = pageSize, BatchSize = 2, MaxPages = maxPages, MaxVehicles = maxVehicles,
                Timeout = timeout ?? TimeSpan.FromHours(2)
            };
            Source.IsPrepared = () => Normalizer.Prepares == 1;
            Handler = new([Source], [Normalizer], new SyncLock(acquire), Writer, new CatalogSyncValidator(TimeProvider.System, options),
                Generations, new UnitOfWork(failOnSaveCall), new Publisher(Generations, publishFailure), FailureRecorder,
                options, TimeProvider.System);
        }
    }

    private sealed class Source : IVehicleCatalogSource
    {
        public string Provider => "Carzin";
        public Queue<CatalogSourcePage> Pages { get; } = new();
        public Exception? Exception { get; set; }
        public bool BlockUntilCanceled { get; set; }
        public int Reads { get; private set; }
        public Func<bool> IsPrepared { get; set; } = () => false;
        public bool PreparedWhenRead { get; private set; }
        public async Task<CatalogSourcePage> ReadPageAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            Reads++;
            PreparedWhenRead = IsPrepared();
            if (Exception is not null) throw Exception;
            if (BlockUntilCanceled) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Pages.Dequeue();
        }
    }

    private sealed class Normalizer : IVehicleCatalogItemNormalizer
    {
        public string Provider => "Carzin";
        public int Prepares { get; private set; }
        public int Normalizations { get; private set; }
        public Exception? PrepareException { get; set; }
        public Func<CatalogSourceVehicle, CatalogItemNormalizationResult> NormalizeResult { get; set; }
            = CatalogItemNormalizationResult.Valid;
        public Task PrepareAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Prepares++;
            if (PrepareException is not null) throw PrepareException;
            return Task.CompletedTask;
        }
        public CatalogItemNormalizationResult Normalize(CatalogSourceVehicle source)
        {
            Normalizations++;
            return NormalizeResult(source);
        }
    }

    private sealed class SyncLock(bool acquire) : IProviderSynchronizationLock
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(string provider, CancellationToken cancellationToken)
            => Task.FromResult<IAsyncDisposable?>(acquire ? new Lease() : null);
        private sealed class Lease : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }

    private sealed class Writer : IVehicleCatalogBatchWriter
    {
        public List<VehicleCatalogItem> Items { get; } = [];
        public Task WriteBatchAsync(IReadOnlyCollection<VehicleCatalogItem> items, CancellationToken cancellationToken)
        { Items.AddRange(items); return Task.CompletedTask; }
    }

    private sealed class GenerationRepository : ICatalogGenerationRepository
    {
        public List<CatalogGeneration> Values { get; } = [];
        public Task AddAsync(CatalogGeneration generation, CancellationToken cancellationToken)
        {
            typeof(CatalogGeneration).GetProperty(nameof(CatalogGeneration.Id))!.SetValue(generation, Values.Count + 1);
            Values.Add(generation); return Task.CompletedTask;
        }
        public Task<CatalogGeneration?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(Values.SingleOrDefault(x => x.Id == id));
        public Task<CatalogGeneration?> GetCurrentByProviderAsync(string provider, CancellationToken cancellationToken) => Task.FromResult(Values.SingleOrDefault(x => x.Status == CatalogGenerationStatus.Current));
        public Task<IReadOnlyCollection<CatalogGeneration>> ListLoadingBeforeAsync(DateTimeOffset createdBeforeUtc, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<CatalogGeneration>>([]);
    }

    private sealed class UnitOfWork(int? failOnSaveCall) : IUnitOfWork
    {
        private int _calls;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (++_calls == failOnSaveCall) throw new InvalidOperationException("save_failed");
            return Task.FromResult(1);
        }
    }
    private sealed class Publisher(GenerationRepository generations, bool fail) : ICatalogPublisher
    {
        public Task<CatalogPublication> PublishAsync(int readyGenerationId, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken)
        {
            if (fail) throw new InvalidOperationException("publish_failed");
            var generation = generations.Values.Single(x => x.Id == readyGenerationId); generation.MarkCurrent(publishedAtUtc);
            return Task.FromResult(new CatalogPublication(publishedAtUtc, generation.ValidItemCount, 1));
        }
    }
    private sealed class FailureRecorder : ICatalogGenerationFailureRecorder
    {
        public int Calls { get; private set; }
        public Task<bool> TryMarkLoadingAsFailedAsync(int generationId, string sanitizedError, DateTimeOffset failedAtUtc, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(false); }
    }
}
