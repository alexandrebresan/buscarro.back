using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using MediatR;
using System.Diagnostics;

namespace Buscarro.Application.Handlers;

public sealed class SynchronizeProviderCatalogHandler(
    IEnumerable<IVehicleCatalogSource> sources,
    IEnumerable<IVehicleCatalogItemNormalizer> normalizers,
    IProviderSynchronizationLock synchronizationLock,
    IVehicleCatalogBatchWriter batchWriter,
    ICatalogSyncValidator validator,
    ICatalogGenerationRepository generations,
    IUnitOfWork unitOfWork,
    ICatalogPublisher publisher,
    ICatalogGenerationFailureRecorder failureRecorder,
    CatalogSynchronizationOptions options,
    TimeProvider timeProvider) : IRequestHandler<SynchronizeProviderCatalogCommand, CatalogSynchronizationResult>
{
    public async Task<CatalogSynchronizationResult> Handle(SynchronizeProviderCatalogCommand request, CancellationToken cancellationToken)
    {
        var started=Stopwatch.GetTimestamp();
        var provider = request.Provider?.Trim() ?? string.Empty;
        var source = sources.SingleOrDefault(value => string.Equals(value.Provider, provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Catalog source '{provider}' is not registered.");
        await using var lease = await synchronizationLock.TryAcquireAsync(source.Provider, cancellationToken);
        if (lease is null) {CatalogMetrics.RecordSync(source.Provider,"lock_unavailable",0,0,0,0,0,Stopwatch.GetElapsedTime(started));return new(source.Provider, false, null, 0, 0, 0, 0);}

        using var deadline = new CancellationTokenSource(options.Timeout, timeProvider);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var normalizer = normalizers.SingleOrDefault(value => string.Equals(value.Provider, source.Provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Catalog item normalizer for '{source.Provider}' is not registered.");
        var generation = new CatalogGeneration(source.Provider, timeProvider.GetUtcNow());
        try
        {
            await normalizer.PrepareAsync(timeout.Token);
            await generations.AddAsync(generation, timeout.Token);
            await unitOfWork.SaveChangesAsync(timeout.Token);
            var result = await LoadAsync(source, normalizer, generation, timeout.Token);
            await publisher.PublishAsync(generation.Id, timeProvider.GetUtcNow(), timeout.Token);
            CatalogMetrics.RecordSync(source.Provider,"success",result.PageCount,result.SourceItemCount,result.ValidItemCount,result.InvalidItemCount,result.DuplicateItemCount,Stopwatch.GetElapsedTime(started));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            CatalogMetrics.RecordTimeout("sync");CatalogMetrics.RecordSync(source.Provider,"timeout",0,0,0,0,0,Stopwatch.GetElapsedTime(started));
            if (generation.Id > 0) await RecordFailureAsync(generation.Id, "catalog_sync_timeout");
            throw new TimeoutException("Catalog synchronization exceeded its configured timeout.", exception);
        }
        catch (Exception exception)
        {
            CatalogMetrics.RecordSync(source.Provider,"failure",0,0,0,0,0,Stopwatch.GetElapsedTime(started));
            if (generation.Id > 0) await RecordFailureAsync(generation.Id, exception.GetType().Name);
            throw;
        }
    }

    private async Task<CatalogSynchronizationResult> LoadAsync(
        IVehicleCatalogSource source,
        IVehicleCatalogItemNormalizer normalizer,
        CatalogGeneration generation,
        CancellationToken token)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var fingerprints = new HashSet<string>(StringComparer.Ordinal);
        var batch = new List<VehicleCatalogItem>(options.BatchSize);
        var reasons = new Dictionary<string, int>(StringComparer.Ordinal);
        using var checksum = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var sourceCount = 0; var valid = 0; var invalid = 0; var duplicates = 0;
        int? stableTotalPages = null; int? stableTotalResults = null; var pageCount = 0;
        bool? hasTotalPages = null, hasTotalResults = null;
        var validationDuration=TimeSpan.Zero;var writeDuration=TimeSpan.Zero;
        var pageSize = source.PageSize ?? options.PageSize;

        for (var pageNumber = 1; pageNumber <= options.MaxPages; pageNumber++)
        {
            var page = await source.ReadPageAsync(pageNumber, pageSize, token);
            pageCount++;
            ValidatePage(page, pageNumber, pageSize, options.MaxPages,
                ref hasTotalPages, ref stableTotalPages, ref hasTotalResults, ref stableTotalResults);
            if (stableTotalPages is int knownTotalPages && pageNumber < knownTotalPages && page.Items.Count < pageSize)
                throw new InvalidOperationException("catalog_page_incomplete_before_final");
            sourceCount += page.Items.Count;
            if (sourceCount > options.MaxVehicles) throw new InvalidOperationException("catalog_vehicle_limit_exceeded");
            var fingerprint = Fingerprint(page.Items);
            if (page.Items.Count > 0 && !fingerprints.Add(fingerprint)) throw new InvalidOperationException("catalog_repeated_page");

            foreach (var sourceItem in page.Items)
            {
                var validationStarted=Stopwatch.GetTimestamp();
                var normalization = normalizer.Normalize(sourceItem);
                if (!normalization.IsValid)
                {
                    validationDuration+=Stopwatch.GetElapsedTime(validationStarted);
                    invalid++; reasons[normalization.Reason!] = reasons.GetValueOrDefault(normalization.Reason!) + 1; continue;
                }
                var item = validator.ValidateAndNormalize(normalization.Vehicle!);
                validationDuration+=Stopwatch.GetElapsedTime(validationStarted);
                if (!item.IsValid)
                {
                    invalid++; reasons[item.Reason!] = reasons.GetValueOrDefault(item.Reason!) + 1; continue;
                }
                var vehicle = item.Vehicle!;
                if (!ids.Add(vehicle.ExternalId!)) { duplicates++; continue; }
                var entity = Map(generation.Id, generation.Provider, vehicle);
                batch.Add(entity); valid++; AppendChecksum(checksum, entity);
                if (batch.Count >= options.BatchSize) { var writeStarted=Stopwatch.GetTimestamp();await batchWriter.WriteBatchAsync(batch, token);writeDuration+=Stopwatch.GetElapsedTime(writeStarted);batch.Clear(); }
            }
            if (page.HasNext is false)
            {
                if (stableTotalPages is int remainingTotalPages && pageNumber < remainingTotalPages)
                    throw new InvalidOperationException("catalog_has_next_missing_before_final");
                if (stableTotalResults is int knownTotalResults && sourceCount < knownTotalResults)
                    throw new InvalidOperationException("catalog_total_results_incomplete");
                break;
            }
            if (stableTotalPages is int finalTotalPages && pageNumber >= finalTotalPages)
            {
                if (page.HasNext is true) throw new InvalidOperationException("catalog_has_next_after_final");
                break;
            }
            if (page.HasNext is true)
            {
                if (pageNumber == options.MaxPages) throw new InvalidOperationException("catalog_page_limit_exceeded");
                continue;
            }
            if (stableTotalPages is int exact ? pageNumber >= exact : page.Items.Count == 0 || page.Items.Count < pageSize) break;
            if (pageNumber == options.MaxPages) throw new InvalidOperationException("catalog_page_limit_exceeded");
        }
        if (batch.Count > 0) {var writeStarted=Stopwatch.GetTimestamp();await batchWriter.WriteBatchAsync(batch, token);writeDuration+=Stopwatch.GetElapsedTime(writeStarted);}
        if (sourceCount == 0) throw new InvalidOperationException("catalog_empty");
        if (valid + invalid + duplicates != sourceCount) throw new InvalidOperationException("catalog_metrics_inconsistent");
        if (hasTotalResults == true && stableTotalResults != sourceCount) throw new InvalidOperationException("catalog_total_results_mismatch");
        var previous = await generations.GetCurrentByProviderAsync(generation.Provider, token);
        var snapshotError = validator.ValidateSnapshot(valid, previous?.ValidItemCount);
        var digest = Convert.ToHexString(checksum.GetHashAndReset()).ToLowerInvariant();
        var summary = JsonSerializer.Serialize(new { reasons });
        if (snapshotError is not null)
        {
            generation.RecordValidationSummary(summary, timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(token);
            throw new InvalidOperationException(snapshotError);
        }
        generation.MarkReady(pageCount, sourceCount, valid, invalid, duplicates, digest, summary, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(token);
        CatalogMetrics.RecordValidation(generation.Provider,validationDuration);CatalogMetrics.RecordWrite(generation.Provider,writeDuration);
        return new(generation.Provider, true, generation.Id, sourceCount, valid, invalid, duplicates,pageCount);
    }

    private static void ValidatePage(
        CatalogSourcePage page,
        int requested,
        int pageSize,
        int maxPages,
        ref bool? hasTotalPages,
        ref int? totalPages,
        ref bool? hasTotalResults,
        ref int? totalResults)
    {
        if (page.RequestedPage != requested || page.ReportedPage is not null && page.ReportedPage != requested) throw new InvalidOperationException("catalog_page_mismatch");
        if (page.Items.Count > pageSize) throw new InvalidOperationException("catalog_page_size_exceeded");
        if (page.CurrentResults is < 0 || page.CurrentResults > pageSize || page.CurrentResults is not null && page.CurrentResults != page.Items.Count)
            throw new InvalidOperationException("catalog_current_results_mismatch");
        if (page.TotalPages is < 1) throw new InvalidOperationException("catalog_total_pages_invalid");
        if (page.TotalPages < requested) throw new InvalidOperationException("catalog_total_pages_before_requested");
        if (page.TotalPages > maxPages) throw new InvalidOperationException("catalog_page_limit_exceeded");
        if (page.TotalResults is < 0) throw new InvalidOperationException("catalog_total_results_invalid");
        if (hasTotalPages is not null && hasTotalPages != page.TotalPages.HasValue) throw new InvalidOperationException("catalog_total_pages_presence_changed");
        if (hasTotalResults is not null && hasTotalResults != page.TotalResults.HasValue) throw new InvalidOperationException("catalog_total_results_presence_changed");
        if (hasTotalPages == true && totalPages != page.TotalPages) throw new InvalidOperationException("catalog_total_pages_changed");
        if (hasTotalResults == true && totalResults != page.TotalResults) throw new InvalidOperationException("catalog_total_results_changed");
        hasTotalPages ??= page.TotalPages.HasValue;
        hasTotalResults ??= page.TotalResults.HasValue;
        if (hasTotalPages == true) totalPages ??= page.TotalPages;
        if (hasTotalResults == true) totalResults ??= page.TotalResults;
    }

    private static VehicleCatalogItem Map(int generationId, string provider, CatalogSourceVehicle source) => new(
        generationId, provider, source.ExternalId!, source.Title!, CatalogSyncValidator.Normalize(source.Title)!)
    {
        Brand = source.Brand, Model = source.Model, Version = source.Version, Year = source.Year, Mileage = source.Mileage,
        Doors = source.Doors, Armored = source.Armored, Fuel = source.Fuel, Transmission = source.Transmission, Color = source.Color,
        Price = source.Price, City = source.City, State = source.State, Url = source.Url, ImageUrl = source.ImageUrl,
        BrandNormalized = CatalogSyncValidator.Normalize(source.Brand), ModelNormalized = CatalogSyncValidator.Normalize(source.Model),
        VersionNormalized = CatalogSyncValidator.Normalize(source.Version), FuelNormalized = CatalogSyncValidator.Normalize(source.Fuel),
        TransmissionNormalized = CatalogSyncValidator.Normalize(source.Transmission), ColorNormalized = CatalogSyncValidator.Normalize(source.Color),
        CityNormalized = CatalogSyncValidator.Normalize(source.City), StateNormalized = CatalogSyncValidator.Normalize(source.State)
    };

    private static string Fingerprint(IEnumerable<CatalogSourceVehicle> items)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var item in items)
        {
            foreach (var value in SourceValues(item)) AppendValue(hash, value);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendChecksum(IncrementalHash hash, VehicleCatalogItem item)
    {
        var length = new byte[4];
        foreach (var value in new[]
        {
            item.Provider, item.ExternalId, item.TitleNormalized,
            item.BrandNormalized, item.ModelNormalized, item.VersionNormalized, item.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            item.Mileage?.ToString(System.Globalization.CultureInfo.InvariantCulture), item.Doors?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            item.Armored?.ToString(), item.FuelNormalized, item.TransmissionNormalized, item.ColorNormalized,
            item.Price?.ToString(System.Globalization.CultureInfo.InvariantCulture), item.CityNormalized, item.StateNormalized, item.Url, item.ImageUrl
        })
            AppendValue(hash, value, length);
    }

    private static IEnumerable<string?> SourceValues(CatalogSourceVehicle item)
    {
        yield return item.ExternalId; yield return item.Title; yield return item.Brand; yield return item.Model; yield return item.Version;
        yield return item.ManufacturerCode; yield return item.ModelCode; yield return item.VersionCode;
        yield return item.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        yield return item.Mileage?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        yield return item.Doors?.ToString(System.Globalization.CultureInfo.InvariantCulture); yield return item.Armored?.ToString();
        yield return item.Fuel; yield return item.Transmission; yield return item.Color;
        yield return item.Price?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        yield return item.City; yield return item.State; yield return item.Url; yield return item.ImageUrl;
    }

    private static void AppendValue(IncrementalHash hash, string? value, byte[]? lengthBuffer = null)
    {
        hash.AppendData(value is null ? [0] : [1]);
        if (value is null) return;
        var bytes = Encoding.UTF8.GetBytes(value);
        var length = lengthBuffer ?? new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length); hash.AppendData(length); hash.AppendData(bytes);
    }

    private async Task RecordFailureAsync(int id, string safeReason)
    {
        using var recorderTimeout = new CancellationTokenSource(options.FailureRecordTimeout, timeProvider);
        _ = await failureRecorder.TryMarkLoadingAsFailedAsync(id, safeReason, timeProvider.GetUtcNow(), recorderTimeout.Token);
    }
}
