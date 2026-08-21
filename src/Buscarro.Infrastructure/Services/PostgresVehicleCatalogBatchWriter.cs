using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Npgsql;
using NpgsqlTypes;

namespace Buscarro.Infrastructure.Services;

public sealed class PostgresVehicleCatalogBatchWriter(NpgsqlDataSource dataSource) : IVehicleCatalogBatchWriter
{
    internal const string CopySql = """
        COPY vehicle_catalog (generation_id, provider, external_id, title, brand, model, version, year, mileage, doors,
        armored, fuel, transmission, color, price, city, state,
        url, image_url, title_normalized, brand_normalized, model_normalized, version_normalized, fuel_normalized,
        transmission_normalized, color_normalized, city_normalized, state_normalized) FROM STDIN (FORMAT BINARY)
        """;

    public async Task WriteBatchAsync(IReadOnlyCollection<VehicleCatalogItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var writer = await connection.BeginBinaryImportAsync(CopySql, cancellationToken))
        {
            foreach (var item in items)
            {
                await writer.StartRowAsync(cancellationToken);
                await writer.WriteAsync(item.GenerationId, NpgsqlDbType.Integer, cancellationToken);
                await WriteText(writer, item.Provider, cancellationToken); await WriteText(writer, item.ExternalId, cancellationToken);
                await WriteText(writer, item.Title, cancellationToken); await WriteText(writer, item.Brand, cancellationToken); await WriteText(writer, item.Model, cancellationToken); await WriteText(writer, item.Version, cancellationToken);
                await WriteNullable(writer, item.Year, NpgsqlDbType.Integer, cancellationToken); await WriteNullable(writer, item.Mileage, NpgsqlDbType.Integer, cancellationToken);
                await WriteNullable(writer, item.Doors, NpgsqlDbType.Integer, cancellationToken); await WriteNullable(writer, item.Armored, NpgsqlDbType.Boolean, cancellationToken);
                await WriteText(writer, item.Fuel, cancellationToken); await WriteText(writer, item.Transmission, cancellationToken); await WriteText(writer, item.Color, cancellationToken);
                await WriteNullable(writer, item.Price, NpgsqlDbType.Numeric, cancellationToken);
                await WriteText(writer, item.City, cancellationToken); await WriteText(writer, item.State, cancellationToken); await WriteText(writer, item.Url, cancellationToken); await WriteText(writer, item.ImageUrl, cancellationToken);
                await WriteText(writer, item.TitleNormalized, cancellationToken); await WriteText(writer, item.BrandNormalized, cancellationToken); await WriteText(writer, item.ModelNormalized, cancellationToken);
                await WriteText(writer, item.VersionNormalized, cancellationToken); await WriteText(writer, item.FuelNormalized, cancellationToken); await WriteText(writer, item.TransmissionNormalized, cancellationToken);
                await WriteText(writer, item.ColorNormalized, cancellationToken); await WriteText(writer, item.CityNormalized, cancellationToken); await WriteText(writer, item.StateNormalized, cancellationToken);
            }
            await writer.CompleteAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task WriteText(NpgsqlBinaryImporter writer, string? value, CancellationToken token)
    {
        if (value is null) await writer.WriteNullAsync(token); else await writer.WriteAsync(value, NpgsqlDbType.Text, token);
    }

    private static async Task WriteNullable<T>(NpgsqlBinaryImporter writer, T? value, NpgsqlDbType type, CancellationToken token) where T : struct
    {
        if (value is null) await writer.WriteNullAsync(token); else await writer.WriteAsync(value.Value, type, token);
    }
}
