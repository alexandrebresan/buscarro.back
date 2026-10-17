using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Buscarro.Infrastructure.Repositories;

public sealed class PostgresGeneralVehicleCatalogReadService(
    NpgsqlDataSource dataSource,
    IOptions<CatalogQueryOptions> options)
    : IGeneralVehicleCatalogReadService
{
    private const string BrandColumn = "brand";
    private const string ModelColumn = "model";
    private const string VersionColumn = "version";
    private const string FuelColumn = "fuel";
    private const string TransmissionColumn = "transmission";
    private const string ColorColumn = "color";

    public Task<IReadOnlyCollection<string>> ListManufacturersAsync(
        CancellationToken cancellationToken)
        => ListNamesAsync(
            BrandColumn,
            null,
            null,
            cancellationToken);

    public Task<IReadOnlyCollection<string>> ListModelsAsync(
        string? manufacturer,
        CancellationToken cancellationToken)
        => ListNamesAsync(
            ModelColumn,
            manufacturer,
            null,
            cancellationToken);

    public Task<IReadOnlyCollection<string>> ListVersionsAsync(
        string model,
        string? manufacturer,
        CancellationToken cancellationToken)
        => ListNamesAsync(
            VersionColumn,
            manufacturer,
            model,
            cancellationToken);

    public Task<IReadOnlyCollection<string>> ListFuelsAsync(
        CancellationToken cancellationToken)
        => ListFuelsAsync(
            null,
            null,
            cancellationToken);

    public Task<IReadOnlyCollection<string>> ListFuelsAsync(
        string? manufacturer,
        string? model,
        CancellationToken cancellationToken)
        => ListNamesAsync(
            FuelColumn,
            manufacturer,
            model,
            cancellationToken);

    public Task<IReadOnlyCollection<string>> ListTransmissionsAsync(
        CancellationToken cancellationToken)
        => ListTransmissionsAsync(
            null,
            null,
            cancellationToken);

    public Task<IReadOnlyCollection<string>> ListTransmissionsAsync(
        string? manufacturer,
        string? model,
        CancellationToken cancellationToken)
        => ListNamesAsync(
            TransmissionColumn,
            manufacturer,
            model,
            cancellationToken);

    public Task<IReadOnlyCollection<string>> ListColorsAsync(
        CancellationToken cancellationToken)
        => ListColorsAsync(
            null,
            null,
            cancellationToken);

    public Task<IReadOnlyCollection<string>> ListColorsAsync(
        string? manufacturer,
        string? model,
        CancellationToken cancellationToken)
        => ListNamesAsync(
            ColorColumn,
            manufacturer,
            model,
            cancellationToken);

    private async Task<IReadOnlyCollection<string>> ListNamesAsync(
        string column,
        string? manufacturer,
        string? model,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var connection =
                await dataSource.OpenConnectionAsync(
                    cancellationToken);

            await using var command = new NpgsqlCommand(
                $"""
                SELECT
                    min(
                        regexp_replace(
                            v.{column},
                            '^[[:space:]]+|[[:space:]]+$',
                            '',
                            'g'
                        ) COLLATE "C"
                    ) AS name
                FROM provider_catalog_current p
                JOIN catalog_generation g
                  ON g.id = p.current_generation_id
                 AND g.provider = p.provider
                 AND g.status = 2
                JOIN vehicle_catalog v
                  ON v.generation_id = g.id
                 AND v.provider = g.provider
                WHERE v.{column} IS NOT NULL
                  AND v.{column}_normalized IS NOT NULL
                  AND v.{column}_normalized <> ''
                  AND v.{column} ~ '[^[:space:]]'
                  AND (
                      @manufacturer IS NULL
                      OR v.brand_normalized = @manufacturer
                  )
                  AND (
                      @model IS NULL
                      OR v.model_normalized = @model
                  )
                GROUP BY v.{column}_normalized
                ORDER BY v.{column}_normalized COLLATE "C"
                """,
                connection)
            {
                CommandTimeout = Math.Max(
                    1,
                    options.Value.CommandTimeoutSeconds)
            };

            AddFilter(
                command,
                "manufacturer",
                manufacturer);

            AddFilter(
                command,
                "model",
                model);

            var names = new List<string>();

            await using var reader =
                await command.ExecuteReaderAsync(
                    cancellationToken);

            while (await reader.ReadAsync(
                       cancellationToken))
            {
                names.Add(reader.GetString(0));
            }

            return names.ToArray();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
            when (ex is NpgsqlException or TimeoutException)
        {
            throw new CatalogUnavailableException(ex);
        }
    }

    private static void AddFilter(
        NpgsqlCommand command,
        string name,
        string? value)
    {
        var normalized =
            string.IsNullOrWhiteSpace(value)
                ? null
                : VehicleCatalogSearchCriteriaNormalizer
                    .NormalizeText(value);

        command.Parameters
            .Add(name, NpgsqlDbType.Text)
            .Value =
                (object?)normalized
                ?? DBNull.Value;
    }
}