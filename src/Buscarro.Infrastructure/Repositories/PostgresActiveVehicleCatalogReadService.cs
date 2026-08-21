using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Buscarro.Infrastructure.Repositories;

public sealed class PostgresActiveVehicleCatalogReadService(
    NpgsqlDataSource dataSource,
    IOptions<CatalogQueryOptions> options) : IActiveVehicleCatalogReadService
{
    public async Task<Vehicle?> FindByProviderAndExternalIdAsync(
        string provider,
        string externalId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                SELECT v.external_id, v.provider, v.title, v.brand, v.model, v.version,
                       v.year, v.mileage, v.doors, v.armored, v.fuel, v.transmission,
                       v.color, v.price, v.city, v.state, v.url, v.image_url
                FROM vehicle_catalog v
                JOIN provider_catalog_current p
                  ON p.provider = v.provider
                 AND p.current_generation_id = v.generation_id
                JOIN catalog_generation g
                  ON g.id = p.current_generation_id
                 AND g.provider = p.provider
                 AND g.status = 2
                WHERE lower(v.provider) = lower(@provider)
                  AND v.external_id = @external_id
                LIMIT 1
                """,
                connection)
            {
                CommandTimeout = Math.Max(1, options.Value.CommandTimeoutSeconds)
            };
            command.Parameters.AddWithValue("provider", provider);
            command.Parameters.AddWithValue("external_id", externalId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var row = new ActiveVehicleCatalogRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                GetReference<string>(reader, 3),
                GetReference<string>(reader, 4),
                GetReference<string>(reader, 5),
                GetNullable<int>(reader, 6),
                GetNullable<int>(reader, 7),
                GetNullable<int>(reader, 8),
                GetNullable<bool>(reader, 9),
                GetReference<string>(reader, 10),
                GetReference<string>(reader, 11),
                GetReference<string>(reader, 12),
                GetNullable<decimal>(reader, 13),
                GetReference<string>(reader, 14),
                GetReference<string>(reader, 15),
                GetReference<string>(reader, 16),
                GetReference<string>(reader, 17));

            return CreateVehicle(row);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            throw new CatalogUnavailableException(ex);
        }
    }

    internal static Vehicle? CreateVehicle(ActiveVehicleCatalogRow row)
    {
        if (row.Year is null || row.Mileage is null || row.Price is null)
        {
            return null;
        }

        return new Vehicle
            {
                ExternalId = row.ExternalId,
                Provider = row.Provider,
                Title = row.Title,
                Brand = row.Brand ?? string.Empty,
                Model = row.Model ?? string.Empty,
                Version = row.Version ?? string.Empty,
                Year = row.Year.Value,
                Mileage = row.Mileage.Value,
                Doors = row.Doors,
                Armored = row.Armored,
                Fuel = row.Fuel ?? string.Empty,
                Transmission = row.Transmission ?? string.Empty,
                Color = row.Color ?? string.Empty,
                Price = row.Price.Value,
                City = row.City ?? string.Empty,
                State = row.State ?? string.Empty,
                Url = row.Url ?? string.Empty,
                ImageUrl = row.ImageUrl ?? string.Empty
            };
    }

    private static T? GetNullable<T>(NpgsqlDataReader reader, int ordinal)
        where T : struct
        => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);

    private static T? GetReference<T>(NpgsqlDataReader reader, int ordinal)
        where T : class
        => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);
}

internal sealed record ActiveVehicleCatalogRow(
    string ExternalId,
    string Provider,
    string Title,
    string? Brand,
    string? Model,
    string? Version,
    int? Year,
    int? Mileage,
    int? Doors,
    bool? Armored,
    string? Fuel,
    string? Transmission,
    string? Color,
    decimal? Price,
    string? City,
    string? State,
    string? Url,
    string? ImageUrl);
