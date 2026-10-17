using System.Data;
using System.Diagnostics;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Buscarro.Infrastructure.Repositories;

public sealed class PostgresVehicleCatalogReadService
    : IVehicleSearchReadService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly CatalogQueryCache _cache;
    private readonly CatalogQueryOptions _options;

    private readonly VehicleCatalogSqlPredicateBuilder _builder =
        new();

    public PostgresVehicleCatalogReadService(
        NpgsqlDataSource dataSource,
        CatalogQueryCache cache,
        IOptions<CatalogQueryOptions> options)
    {
        _dataSource = dataSource;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<VehicleCatalogSearchResult> SearchAsync(
        NormalizedVehicleCatalogSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resultKey =
            CatalogQueryCache.BuildResultKey(criteria);

        if (_cache.TryGetResult(
                resultKey,
                out var cachedResult))
        {
            return cachedResult;
        }

        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(
                    cancellationToken);

            await using var transaction =
                await connection.BeginTransactionAsync(
                    IsolationLevel.RepeatableRead,
                    cancellationToken);

            await using (var readOnly =
                         new NpgsqlCommand(
                             "SET TRANSACTION READ ONLY",
                             connection,
                             transaction))
            {
                await readOnly.ExecuteNonQueryAsync(
                    cancellationToken);
            }

            var metadataStarted =
                Stopwatch.GetTimestamp();

            var publication =
                await ReadPublicationAsync(
                    connection,
                    transaction,
                    cancellationToken)
                ?? throw new CatalogUnavailableException();

            var pointers =
                await ReadPointersAsync(
                    connection,
                    transaction,
                    cancellationToken);

            CatalogMetrics.RecordQueryStage(
                "metadata",
                Stopwatch.GetElapsedTime(
                    metadataStarted));

            if (pointers.Count == 0)
            {
                throw new CatalogUnavailableException();
            }

            /*
             * Primeiro aplicamos o filtro explícito de provider,
             * caso exista.
             *
             * Em seguida aplicamos o País.
             *
             * Country não é armazenado como coluna no banco:
             * ele é determinado pelo provider através de
             * ProviderCountryCatalog.
             */
            var selected = pointers
                .Where(pointer =>
                    criteria.Providers.Count == 0 ||
                    criteria.Providers.Contains(
                        VehicleCatalogSearchCriteriaNormalizer
                            .NormalizeText(pointer.Provider),
                        StringComparer.Ordinal))
                .Where(pointer =>
                    ProviderCountryCatalog
                        .ProviderMatchesCountries(
                            pointer.Provider,
                            criteria.Countries))
                .ToArray();

            var generationIds =
                selected
                    .Select(pointer =>
                        pointer.GenerationId)
                    .ToArray();

            var providerNames =
                selected
                    .Select(pointer =>
                        pointer.Provider)
                    .ToArray();

            var predicate =
                _builder.Build(
                    criteria,
                    generationIds,
                    providerNames);

            var countKey =
                CatalogQueryCache.BuildCountKey(
                    publication.Id,
                    criteria);

            var pageKey =
                CatalogQueryCache.BuildPageKey(
                    publication.Id,
                    criteria);

            var countCached =
                _cache.TryGetCount(
                    countKey,
                    out var totalCount);

            if (!countCached)
            {
                var countStarted =
                    Stopwatch.GetTimestamp();

                totalCount =
                    await ReadCountAsync(
                        connection,
                        transaction,
                        predicate,
                        cancellationToken);

                CatalogMetrics.RecordQueryStage(
                    "count",
                    Stopwatch.GetElapsedTime(
                        countStarted));

                _cache.SetCount(
                    countKey,
                    totalCount);
            }

            var pageCached =
                _cache.TryGetPage(
                    pageKey,
                    out var page);

            if (!pageCached)
            {
                var pageStarted =
                    Stopwatch.GetTimestamp();

                page =
                    await ReadPageAsync(
                        connection,
                        transaction,
                        predicate,
                        criteria,
                        cancellationToken);

                CatalogMetrics.RecordQueryStage(
                    "page",
                    Stopwatch.GetElapsedTime(
                        pageStarted));

                _cache.SetPage(
                    pageKey,
                    page);
            }

            await transaction.CommitAsync(
                cancellationToken);

            var totalPages =
                totalCount == 0
                    ? 0
                    : checked(
                        (int)(
                            (
                                totalCount +
                                (long)criteria.PageSize -
                                1
                            )
                            /
                            criteria.PageSize
                        )
                    );

            var result =
                new VehicleCatalogSearchResult
                {
                    Vehicles =
                        page.Vehicles,

                    Providers =
                        selected
                            .OrderBy(
                                pointer =>
                                    pointer.Provider,
                                StringComparer.OrdinalIgnoreCase)
                            .Select(
                                pointer =>
                                    new ProviderStatusResponse
                                    {
                                        Provider =
                                            pointer.Provider,

                                        Success =
                                            true,

                                        FromCache =
                                            pageCached,

                                        UsedStaleCache =
                                            false,

                                        CurrentResults =
                                            page.ProviderCounts
                                                .GetValueOrDefault(
                                                    pointer.Provider),

                                        CurrentPage =
                                            criteria.Page
                                    })
                            .ToArray(),

                    PublicationId =
                        publication.Id,

                    CatalogUpdatedAtUtc =
                        publication.PublishedAtUtc,

                    TotalCount =
                        totalCount,

                    Page =
                        criteria.Page,

                    PageSize =
                        criteria.PageSize,

                    TotalPages =
                        totalPages
                };

            _cache.SetResult(
                resultKey,
                result);

            return CatalogSearchResultSnapshot.Copy(
                result);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CatalogUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
            when (ex is NpgsqlException or TimeoutException)
        {
            throw new CatalogUnavailableException(
                innerException: ex);
        }
    }

    private async Task<Publication?> ReadPublicationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command =
            Command(
                """
                SELECT
                    id,
                    published_at_utc,
                    total_catalog_count
                FROM catalog_publication
                ORDER BY id DESC
                LIMIT 1
                """,
                connection,
                transaction);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        return await reader.ReadAsync(
            cancellationToken)
            ? new Publication(
                reader.GetInt32(0),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetInt32(2))
            : null;
    }

    private async Task<IReadOnlyList<Pointer>>
        ReadPointersAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.provider,
                p.current_generation_id
            FROM provider_catalog_current p
            JOIN catalog_generation g
              ON g.id = p.current_generation_id
             AND g.provider = p.provider
            WHERE g.status = 2
            ORDER BY
                lower(p.provider),
                p.provider
            """;

        await using var command =
            Command(
                sql,
                connection,
                transaction);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        var result =
            new List<Pointer>();

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            result.Add(
                new Pointer(
                    reader.GetString(0),
                    reader.GetInt32(1)));
        }

        return result;
    }

    private async Task<int> ReadCountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        VehicleCatalogSqlPredicate predicate,
        CancellationToken cancellationToken)
    {
        await using var command =
            Command(
                "SELECT count(*) FROM vehicle_catalog v WHERE " +
                predicate.WhereSql,
                connection,
                transaction);

        Add(
            command,
            predicate);

        return CheckedCount(
            (long)(
                await command.ExecuteScalarAsync(
                    cancellationToken)
                ?? 0L
            ));
    }

    private async Task<CatalogCachedPage> ReadPageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        VehicleCatalogSqlPredicate predicate,
        NormalizedVehicleCatalogSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        const string columns = """
            v.external_id,
            v.provider,
            v.title,
            v.brand,
            v.model,
            v.version,
            v.year,
            v.mileage,
            v.doors,
            v.armored,
            v.fuel,
            v.transmission,
            v.color,
            v.price,
            v.city,
            v.state,
            v.url,
            v.image_url
            """;

        var sql =
            $"""
             SELECT
                 {columns}
             FROM vehicle_catalog v
             WHERE {predicate.WhereSql}
             ORDER BY {
                 VehicleCatalogSqlPredicateBuilder
                     .OrderBy(criteria.Order)
             }
             LIMIT @page_size
             OFFSET @offset
             """;

        await using var command =
            Command(
                sql,
                connection,
                transaction);

        Add(
            command,
            predicate);

        command.Parameters.AddWithValue(
            "page_size",
            criteria.PageSize);

        command.Parameters.AddWithValue(
            "offset",
            checked(
                (long)(criteria.Page - 1) *
                criteria.PageSize));

        var vehicles =
            new List<VehicleResponse>();

        var counts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            var provider =
                reader.GetString(1);

            counts[provider] =
                counts.GetValueOrDefault(provider) + 1;

            vehicles.Add(
                new VehicleResponse
                {
                    ExternalId =
                        reader.GetString(0),

                    Provider =
                        provider,

                    Title =
                        reader.GetString(2),

                    Brand =
                        Get<string>(
                            reader,
                            3),

                    Model =
                        Get<string>(
                            reader,
                            4),

                    Version =
                        Get<string>(
                            reader,
                            5),

                    Year =
                        Get<int>(
                            reader,
                            6),

                    Mileage =
                        Get<int>(
                            reader,
                            7),

                    Doors =
                        Get<int>(
                            reader,
                            8),

                    Armored =
                        Get<bool>(
                            reader,
                            9),

                    Fuel =
                        Get<string>(
                            reader,
                            10),

                    Transmission =
                        Get<string>(
                            reader,
                            11),

                    Color =
                        Get<string>(
                            reader,
                            12),

                    Price =
                        Get<decimal>(
                            reader,
                            13),

                    City =
                        Get<string>(
                            reader,
                            14),

                    State =
                        Get<string>(
                            reader,
                            15),

                    Url =
                        Get<string>(
                            reader,
                            16),

                    ImageUrl =
                        Get<string>(
                            reader,
                            17)
                });
        }

        return new CatalogCachedPage(
            vehicles,
            counts);
    }

    private NpgsqlCommand Command(
        string sql,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        return new NpgsqlCommand(
            sql,
            connection,
            transaction)
        {
            CommandTimeout =
                Math.Max(
                    1,
                    _options.CommandTimeoutSeconds)
        };
    }

    private static void Add(
        NpgsqlCommand command,
        VehicleCatalogSqlPredicate predicate)
    {
        foreach (var source in predicate.Parameters)
        {
            command.Parameters.Add(
                new NpgsqlParameter(
                    source.ParameterName,
                    source.NpgsqlDbType)
                {
                    Value =
                        source.Value
                });
        }
    }

    private static T? Get<T>(
        NpgsqlDataReader reader,
        int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? default
            : reader.GetFieldValue<T>(ordinal);
    }

    internal static int CheckedCount(
        long value)
    {
        return checked((int)value);
    }

    private sealed record Publication(
        int Id,
        DateTimeOffset PublishedAtUtc,
        int TotalCatalogCount);

    private sealed record Pointer(
        string Provider,
        int GenerationId);
}