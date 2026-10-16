using System.Data;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Diagnostics;

namespace Buscarro.Infrastructure.Repositories;

public sealed class PostgresVehicleCatalogReadService : IVehicleSearchReadService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly CatalogQueryCache _cache;
    private readonly CatalogQueryOptions _options;
    private readonly VehicleCatalogSqlPredicateBuilder _builder = new();

    public PostgresVehicleCatalogReadService(NpgsqlDataSource dataSource, CatalogQueryCache cache, IOptions<CatalogQueryOptions> options)
    { _dataSource = dataSource; _cache = cache; _options = options.Value; }

    public async Task<VehicleCatalogSearchResult> SearchAsync(NormalizedVehicleCatalogSearchCriteria criteria, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resultKey = CatalogQueryCache.BuildResultKey(criteria);
        if (_cache.TryGetResult(resultKey, out var cachedResult)) return cachedResult;

        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
                await readOnly.ExecuteNonQueryAsync(cancellationToken);

            var metadataStarted=Stopwatch.GetTimestamp();
            var publication = await ReadPublicationAsync(connection, transaction, cancellationToken)
                ?? throw new CatalogUnavailableException();
            var pointers = await ReadPointersAsync(connection, transaction, cancellationToken);
            CatalogMetrics.RecordQueryStage("metadata",Stopwatch.GetElapsedTime(metadataStarted));
            if (pointers.Count == 0) throw new CatalogUnavailableException();

            var selected = criteria.Providers.Count == 0
                ? pointers
                : pointers.Where(p => criteria.Providers.Contains(VehicleCatalogSearchCriteriaNormalizer.NormalizeText(p.Provider), StringComparer.Ordinal)).ToArray();
            var generationIds = selected.Select(p => p.GenerationId).ToArray();
            var providerNames = selected.Select(p => p.Provider).ToArray();
            var predicate = _builder.Build(criteria, generationIds, providerNames);
            var countKey = CatalogQueryCache.BuildCountKey(publication.Id, criteria);
            var pageKey = CatalogQueryCache.BuildPageKey(publication.Id, criteria);
            var countCached = _cache.TryGetCount(countKey, out var totalCount);
            if (!countCached)
            {
                var countStarted=Stopwatch.GetTimestamp();
                totalCount = await ReadCountAsync(connection, transaction, predicate, cancellationToken);
                CatalogMetrics.RecordQueryStage("count",Stopwatch.GetElapsedTime(countStarted));
                _cache.SetCount(countKey, totalCount);
            }
            var pageCached = _cache.TryGetPage(pageKey, out var page);
            if (!pageCached)
            {
                var pageStarted=Stopwatch.GetTimestamp();
                page = await ReadPageAsync(connection, transaction, predicate, criteria, cancellationToken);
                CatalogMetrics.RecordQueryStage("page",Stopwatch.GetElapsedTime(pageStarted));
                _cache.SetPage(pageKey, page);
            }
            await transaction.CommitAsync(cancellationToken);
            var totalPages = totalCount == 0
                ? 0
                : checked((int)((totalCount + (long)criteria.PageSize - 1) / criteria.PageSize));
            var result = new VehicleCatalogSearchResult
            {
                Vehicles = page.Vehicles,
                Providers = selected.OrderBy(p => p.Provider, StringComparer.OrdinalIgnoreCase).Select(p => new ProviderStatusResponse
                {
                    Provider = p.Provider, Success = true, FromCache = pageCached, UsedStaleCache = false,
                    CurrentResults = page.ProviderCounts.GetValueOrDefault(p.Provider), CurrentPage = criteria.Page
                }).ToArray(),
                PublicationId = publication.Id, CatalogUpdatedAtUtc = publication.PublishedAtUtc,
                TotalCount = totalCount,
                Page = criteria.Page, PageSize = criteria.PageSize, TotalPages = totalPages
            };
            _cache.SetResult(resultKey, result);
            return CatalogSearchResultSnapshot.Copy(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (CatalogUnavailableException) { throw; }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        { throw new CatalogUnavailableException(innerException: ex); }
    }

    private async Task<Publication?> ReadPublicationAsync(NpgsqlConnection c, NpgsqlTransaction tx, CancellationToken token)
    {
        await using var cmd = Command("SELECT id, published_at_utc, total_catalog_count FROM catalog_publication ORDER BY id DESC LIMIT 1", c, tx);
        await using var reader = await cmd.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new(reader.GetInt32(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetInt32(2)) : null;
    }
    private async Task<IReadOnlyList<Pointer>> ReadPointersAsync(NpgsqlConnection c, NpgsqlTransaction tx, CancellationToken token)
    {
        const string sql = "SELECT p.provider, p.current_generation_id FROM provider_catalog_current p JOIN catalog_generation g ON g.id=p.current_generation_id AND g.provider=p.provider WHERE g.status=2 ORDER BY lower(p.provider), p.provider";
        await using var cmd = Command(sql, c, tx); await using var reader = await cmd.ExecuteReaderAsync(token); var result = new List<Pointer>();
        while (await reader.ReadAsync(token)) result.Add(new(reader.GetString(0), reader.GetInt32(1)));
        return result;
    }
    private async Task<int> ReadCountAsync(NpgsqlConnection c, NpgsqlTransaction tx, VehicleCatalogSqlPredicate p, CancellationToken token)
    {
        await using var cmd = Command("SELECT count(*) FROM vehicle_catalog v WHERE " + p.WhereSql, c, tx); Add(cmd, p);
        return CheckedCount((long)(await cmd.ExecuteScalarAsync(token) ?? 0L));
    }
    private async Task<CatalogCachedPage> ReadPageAsync(NpgsqlConnection c, NpgsqlTransaction tx, VehicleCatalogSqlPredicate p, NormalizedVehicleCatalogSearchCriteria criteria, CancellationToken token)
    {
        const string columns = "v.external_id,v.provider,v.title,v.brand,v.model,v.version,v.year,v.mileage,v.doors,v.armored,v.fuel,v.transmission,v.color,v.price,v.city,v.state,v.url,v.image_url";
        var sql = $"SELECT {columns} FROM vehicle_catalog v WHERE {p.WhereSql} ORDER BY {VehicleCatalogSqlPredicateBuilder.OrderBy(criteria.Order)} LIMIT @page_size OFFSET @offset";
        await using var cmd = Command(sql, c, tx); Add(cmd, p); cmd.Parameters.AddWithValue("page_size", criteria.PageSize); cmd.Parameters.AddWithValue("offset", checked((long)(criteria.Page - 1) * criteria.PageSize));
        var vehicles = new List<VehicleResponse>(); var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var provider = reader.GetString(1); counts[provider] = counts.GetValueOrDefault(provider) + 1;
            vehicles.Add(new VehicleResponse { ExternalId=reader.GetString(0),Provider=provider,Title=reader.GetString(2),Brand=Get<string>(reader,3),Model=Get<string>(reader,4),Version=Get<string>(reader,5),Year=Get<int>(reader,6),Mileage=Get<int>(reader,7),Doors=Get<int>(reader,8),Armored=Get<bool>(reader,9),Fuel=Get<string>(reader,10),Transmission=Get<string>(reader,11),Color=Get<string>(reader,12),Price=Get<decimal>(reader,13),City=Get<string>(reader,14),State=Get<string>(reader,15),Url=Get<string>(reader,16),ImageUrl=Get<string>(reader,17) });
        }
        return new(vehicles, counts);
    }
    private NpgsqlCommand Command(string sql, NpgsqlConnection c, NpgsqlTransaction tx) => new(sql,c,tx) { CommandTimeout = Math.Max(1,_options.CommandTimeoutSeconds) };
    private static void Add(NpgsqlCommand cmd, VehicleCatalogSqlPredicate p) { foreach (var source in p.Parameters) cmd.Parameters.Add(new NpgsqlParameter(source.ParameterName, source.NpgsqlDbType) { Value = source.Value }); }
    private static T? Get<T>(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? default : reader.GetFieldValue<T>(ordinal);
    internal static int CheckedCount(long value) => checked((int)value);
    private sealed record Publication(int Id, DateTimeOffset PublishedAtUtc, int TotalCatalogCount);
    private sealed record Pointer(string Provider, int GenerationId);
}
