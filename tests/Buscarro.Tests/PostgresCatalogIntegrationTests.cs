using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Repositories;
using Buscarro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Buscarro.Tests;

public sealed class PostgresCatalogIntegrationTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Versioned_catalog_migration_applies_required_checks_and_gin_index()
    {
        await using var fixture=new PostgresTestDatabaseFixture();
        await fixture.InitializeAsync();
        var connectionString = fixture.ConnectionString;

        var options = new DbContextOptionsBuilder<BuscarroDbContext>()
            .UseNpgsql(PostgresConnectionString.Normalize(connectionString))
            .Options;
        await using var context = new BuscarroDbContext(options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = CreateMigrationMetadataCommand(connection,fixture.Schema);

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3L, reader.GetInt64(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.Equal(3L, reader.GetInt64(2));
    }

    internal static NpgsqlCommand CreateMigrationMetadataCommand(NpgsqlConnection connection,string schema)
    {
        var command=new NpgsqlCommand("""
            SELECT
                (SELECT count(*) FROM pg_constraint c
                 JOIN pg_namespace n ON n.oid = c.connamespace
                 WHERE n.nspname = @schema
                   AND c.conname IN (
                    'ck_catalog_generation_provider_not_blank',
                    'ck_vehicle_catalog_required_text',
                    'ck_provider_catalog_current_provider_not_blank')),
                (SELECT count(*) FROM pg_indexes
                 WHERE schemaname = @schema
                   AND tablename = 'vehicle_catalog'
                   AND indexname = 'ix_vehicle_catalog_search_vector'
                   AND indexdef ILIKE '%USING gin%'),
                (SELECT count(*) FROM information_schema.columns
                 WHERE table_schema = @schema
                   AND table_name = 'vehicle_catalog'
                   AND column_name IN ('year', 'mileage', 'price')
                   AND is_nullable = 'YES')
            """,connection);
        command.Parameters.AddWithValue("schema",schema);return command;
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Binary_copy_preserves_nulls_and_generates_search_vector()
    {
        await using var fixture=new PostgresTestDatabaseFixture();await fixture.InitializeAsync();
        await using var context=await MigratedContextAsync(fixture);
        var provider=Provider(fixture);var generation=new CatalogGeneration(provider,DateTimeOffset.UtcNow);context.Add(generation);await context.SaveChangesAsync();
        await using var dataSource=NpgsqlDataSource.Create(fixture.ConnectionString);
        var item=new VehicleCatalogItem(generation.Id,provider,"copy-1","Fiat 500","fiat 500"){Model="500",ModelNormalized="500"};
        await new PostgresVehicleCatalogBatchWriter(dataSource).WriteBatchAsync([item],TestContext.Current.CancellationToken);
        await using var connection=await dataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command=new NpgsqlCommand("SELECT year IS NULL, price IS NULL, search_vector @@ plainto_tsquery('simple','fiat') FROM vehicle_catalog WHERE external_id='copy-1'",connection);
        await using var reader=await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));Assert.True(reader.GetBoolean(1));Assert.True(reader.GetBoolean(2));
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Advisory_lock_excludes_same_provider_allows_different_and_releases_on_dispose()
    {
        await using var fixture=new PostgresTestDatabaseFixture();await fixture.InitializeAsync();
        await using var dataSource=NpgsqlDataSource.Create(fixture.ConnectionString);var service=new PostgresProviderSynchronizationLock(dataSource);
        var provider=Provider(fixture);var first=await service.TryAcquireAsync(provider,TestContext.Current.CancellationToken);Assert.NotNull(first);
        await using var blocked=await service.TryAcquireAsync(provider,TestContext.Current.CancellationToken);Assert.Null(blocked);
        await using(var other=await service.TryAcquireAsync(provider+"_other",TestContext.Current.CancellationToken))Assert.NotNull(other);
        await first!.DisposeAsync();
        await using var reacquired=await service.TryAcquireAsync(provider,TestContext.Current.CancellationToken);Assert.NotNull(reacquired);
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Publish_successive_and_exact_rollback_create_increasing_publications_and_reject_repeat_target()
    {
        await using var fixture=new PostgresTestDatabaseFixture();await fixture.InitializeAsync();await using var context=await MigratedContextAsync(fixture);
        var provider=Provider(fixture);var now=DateTimeOffset.UtcNow;
        var first=await AddReadyAsync(context,provider,1,now);var publisher=new PostgresCatalogPublisher(context);
        var publication1=await publisher.PublishAsync(first.Id,now.AddSeconds(1),TestContext.Current.CancellationToken);
        var second=await AddReadyAsync(context,provider,2,now.AddSeconds(2));var publication2=await publisher.PublishAsync(second.Id,now.AddSeconds(3),TestContext.Current.CancellationToken);
        var rollback=await publisher.RollbackAsync(provider,first.Id,now.AddSeconds(4),TestContext.Current.CancellationToken);
        Assert.True(publication2.Id>publication1.Id);Assert.True(rollback.PublicationId>publication2.Id);Assert.Equal(first.Id,rollback.CurrentGenerationId);Assert.Equal(second.Id,rollback.PreviousGenerationId);
        await Assert.ThrowsAsync<ConflictException>(()=>publisher.RollbackAsync(provider,first.Id,now.AddSeconds(5),TestContext.Current.CancellationToken));
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Reader_returns_consistent_count_page_publication_and_beyond_end()
    {
        await using var fixture=new PostgresTestDatabaseFixture();await fixture.InitializeAsync();await using var context=await MigratedContextAsync(fixture);
        var provider=Provider(fixture);var now=DateTimeOffset.UtcNow;var generation=await AddReadyAsync(context,provider,2,now);
        await using var dataSource=NpgsqlDataSource.Create(fixture.ConnectionString);var writer=new PostgresVehicleCatalogBatchWriter(dataSource);
        await writer.WriteBatchAsync([
            new VehicleCatalogItem(generation.Id,provider,"reader-1","Fiat 500","fiat 500"){Brand="Fiat",BrandNormalized="fiat"},
            new VehicleCatalogItem(generation.Id,provider,"reader-2","Fiat 600","fiat 600"){Brand="Fiat",BrandNormalized="fiat"}],TestContext.Current.CancellationToken);
        var publication=await new PostgresCatalogPublisher(context).PublishAsync(generation.Id,now.AddSeconds(1),TestContext.Current.CancellationToken);
        var options=Options.Create(new CatalogQueryOptions{CommandTimeoutSeconds=3,CacheSizeLimit=100});using var cache=new CatalogQueryCache(options);
        var service=new PostgresVehicleCatalogReadService(dataSource,cache,options);
        var first=await service.SearchAsync(Criteria(provider,1,1),TestContext.Current.CancellationToken);
        var beyond=await service.SearchAsync(Criteria(provider,3,1),TestContext.Current.CancellationToken);
        Assert.Equal(publication.Id,first.PublicationId);Assert.Equal(2,first.TotalCount);Assert.Single(first.Vehicles);Assert.Equal(2,first.TotalPages);
        Assert.Equal(publication.Id,beyond.PublicationId);Assert.Equal(2,beyond.TotalCount);Assert.Empty(beyond.Vehicles);
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Active_vehicle_lookup_reads_only_the_published_provider_generation()
    {
        await using var fixture = new PostgresTestDatabaseFixture();
        await fixture.InitializeAsync();
        await using var context = await MigratedContextAsync(fixture);
        var provider = Provider(fixture);
        var now = DateTimeOffset.UtcNow;
        var previous = await AddReadyAsync(context, provider, 1, now);
        var current = await AddReadyAsync(context, provider, 1, now.AddSeconds(1));
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);
        var writer = new PostgresVehicleCatalogBatchWriter(dataSource);
        await writer.WriteBatchAsync(
            [new VehicleCatalogItem(previous.Id, provider, "same-id", "Previous", "previous")
                { Year = 2020, Mileage = 10, Price = 100m }],
            TestContext.Current.CancellationToken);
        await writer.WriteBatchAsync(
            [new VehicleCatalogItem(current.Id, provider, "same-id", "Current", "current")
                { Year = 2021, Mileage = 20, Price = 200m }],
            TestContext.Current.CancellationToken);
        var publisher = new PostgresCatalogPublisher(context);
        await publisher.PublishAsync(previous.Id, now.AddSeconds(2), TestContext.Current.CancellationToken);
        await publisher.PublishAsync(current.Id, now.AddSeconds(3), TestContext.Current.CancellationToken);
        var lookup = new PostgresActiveVehicleCatalogReadService(
            dataSource,
            Options.Create(new CatalogQueryOptions()));

        var vehicle = await lookup.FindByProviderAndExternalIdAsync(
            provider,
            "same-id",
            TestContext.Current.CancellationToken);

        Assert.NotNull(vehicle);
        Assert.Equal("Current", vehicle.Title);
    }

    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Cleanup_preserves_current_previous_and_recent_loading_but_removes_expired_unreferenced()
    {
        await using var fixture=new PostgresTestDatabaseFixture();await fixture.InitializeAsync();await using var context=await MigratedContextAsync(fixture);
        var provider=Provider(fixture);var now=DateTimeOffset.UtcNow;var currentCandidate=await AddReadyAsync(context,provider,1,now.AddHours(-3));var publisher=new PostgresCatalogPublisher(context);
        await publisher.PublishAsync(currentCandidate.Id,now.AddHours(-2),TestContext.Current.CancellationToken);
        var current=await AddReadyAsync(context,provider,1,now.AddHours(-1));await publisher.PublishAsync(current.Id,now.AddMinutes(-30),TestContext.Current.CancellationToken);
        var recentLoading=new CatalogGeneration(provider+"_loading",now);var expiredReady=new CatalogGeneration(provider+"_ready",now.AddDays(-3));expiredReady.MarkReady(1,1,1,0,0,new string('a',64),null,now.AddDays(-3));
        var expiredFailed=new CatalogGeneration(provider+"_failed",now.AddDays(-3));expiredFailed.MarkFailed("safe",now.AddDays(-3));context.AddRange(recentLoading,expiredReady,expiredFailed);await context.SaveChangesAsync();
        context.Add(new VehicleCatalogItem(expiredFailed.Id,expiredFailed.Provider,"failed-1","Failed","failed"));await context.SaveChangesAsync();
        var retention=new CatalogRetentionRepository(context);var rows=await retention.DeleteVehicleRowsForFailedGenerationsAsync(TestContext.Current.CancellationToken);
        var removed=await retention.DeleteUnreferencedAsync(now.AddDays(-1),now.AddDays(-1),TestContext.Current.CancellationToken);await context.SaveChangesAsync();
        Assert.Equal(1,rows);Assert.Equal(2,removed);
        var remaining=await context.CatalogGenerations.AsNoTracking().Select(x=>x.Id).ToListAsync();
        Assert.Contains(current.Id,remaining);Assert.Contains(currentCandidate.Id,remaining);Assert.Contains(recentLoading.Id,remaining);Assert.DoesNotContain(expiredReady.Id,remaining);Assert.DoesNotContain(expiredFailed.Id,remaining);
    }

    private static async Task<BuscarroDbContext> MigratedContextAsync(PostgresTestDatabaseFixture fixture)
    {
        var options=new DbContextOptionsBuilder<BuscarroDbContext>().UseNpgsql(PostgresConnectionString.Normalize(fixture.ConnectionString)).Options;
        var context=new BuscarroDbContext(options);await context.Database.MigrateAsync(TestContext.Current.CancellationToken);return context;
    }

    private static async Task<CatalogGeneration> AddReadyAsync(BuscarroDbContext context,string provider,int count,DateTimeOffset now)
    {
        var generation=new CatalogGeneration(provider,now);context.Add(generation);await context.SaveChangesAsync();
        generation.MarkReady(1,count,count,0,0,new string('a',64),null,now);await context.SaveChangesAsync();return generation;
    }

    private static string Provider(PostgresTestDatabaseFixture fixture)=>"p_"+fixture.Schema;
    private static NormalizedVehicleCatalogSearchCriteria Criteria(string provider,int page,int size)=>new(null,[provider.ToLowerInvariant()],[],[],[],[],[],[],[],[],null,null,null,null,null,page,size);
}

internal sealed class PostgresIntegrationFactAttribute : FactAttribute
{
    public PostgresIntegrationFactAttribute()
    {
        if (!PostgresTestDatabaseFixture.CanRun(out var reason))
        {
            Skip = $"Integracao PostgreSQL real NAO EXECUTADA: {reason}.";
        }
    }
}
