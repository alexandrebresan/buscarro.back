using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Buscarro.Application.Interfaces;
using Npgsql;
using Buscarro.Application.Services;
using System.Diagnostics;

namespace Buscarro.Infrastructure.Services;

public sealed class PostgresProviderSynchronizationLock(NpgsqlDataSource dataSource) : IProviderSynchronizationLock
{
    private const int Namespace = 0x42555343;

    public async Task<IAsyncDisposable?> TryAcquireAsync(string provider, CancellationToken cancellationToken)
    {
        var started=Stopwatch.GetTimestamp();
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock($1, $2)", connection);
            var key = ProviderKey(provider);
            command.Parameters.AddWithValue(Namespace);
            command.Parameters.AddWithValue(key);
            var acquired=await command.ExecuteScalarAsync(cancellationToken) is true;
            CatalogMetrics.RecordLock(provider,acquired?"acquired":"unavailable",Stopwatch.GetElapsedTime(started));
            return acquired?new Lease(connection,key):await CloseAsync(connection);
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    internal static int ProviderKey(string provider)
    {
        var normalized = provider.Trim().ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(0, 4));
    }

    private static async Task<IAsyncDisposable?> CloseAsync(NpgsqlConnection connection) { await connection.DisposeAsync(); return null; }

    private sealed class Lease(NpgsqlConnection connection, int key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock($1, $2)", connection);
                command.Parameters.AddWithValue(Namespace); command.Parameters.AddWithValue(key);
                await command.ExecuteNonQueryAsync();
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}
