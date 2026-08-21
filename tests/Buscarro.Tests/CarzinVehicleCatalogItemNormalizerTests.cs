using Buscarro.Application.DTOs;
using Buscarro.Infrastructure.Providers;

namespace Buscarro.Tests;

public sealed class CarzinVehicleCatalogItemNormalizerTests
{
    [Fact]
    public async Task Prepare_loads_each_taxonomy_once_and_normalizing_repeated_items_does_not_read_again()
    {
        var reader = TaxonomyReader.Valid();
        var normalizer = new CarzinVehicleCatalogItemNormalizer(reader);

        await normalizer.PrepareAsync(TestContext.Current.CancellationToken);
        var first = normalizer.Normalize(Source());
        var second = normalizer.Normalize(Source() with { ExternalId = "2" });

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.Equal((1, 1, 1), reader.Calls);
        Assert.Equal(3, reader.QueryCount);
        Assert.Equal("Honda", first.Vehicle!.Brand);
        Assert.Equal("Civic", first.Vehicle.Model);
        Assert.Equal("EXL", first.Vehicle.Version);
        Assert.Equal("Prata", first.Vehicle.Color);
        Assert.Equal("Manual", first.Vehicle.Transmission);
        Assert.Equal("Gasolina e Álcool", first.Vehicle.Fuel);
    }

    [Fact]
    public async Task Normalize_enforces_parent_hierarchy_and_returns_only_sanitized_reason_code()
    {
        var normalizer = new CarzinVehicleCatalogItemNormalizer(TaxonomyReader.Valid());
        await normalizer.PrepareAsync(TestContext.Current.CancellationToken);

        var result = normalizer.Normalize(Source() with { ManufacturerCode = "99" });

        Assert.False(result.IsValid);
        Assert.Null(result.Vehicle);
        Assert.Equal("manufacturer_code_unknown", result.Reason);
        Assert.DoesNotContain("99", result.Reason);
    }

    [Theory]
    [InlineData(null, "2", "3", "manufacturer_code_missing")]
    [InlineData("1", null, "3", "model_code_missing")]
    [InlineData("1", "999", "3", "model_code_unknown")]
    [InlineData("1", "2", "999", "version_code_unknown")]
    public async Task Normalize_rejects_missing_required_or_unknown_hierarchical_codes(
        string? manufacturer, string? model, string? version, string reason)
    {
        var normalizer = new CarzinVehicleCatalogItemNormalizer(TaxonomyReader.Valid());
        await normalizer.PrepareAsync(TestContext.Current.CancellationToken);

        var result = normalizer.Normalize(Source() with
        {
            ManufacturerCode = manufacturer,
            ModelCode = model,
            VersionCode = version
        });

        Assert.False(result.IsValid);
        Assert.Equal(reason, result.Reason);
    }

    [Fact]
    public async Task Optional_version_and_free_form_attribute_names_are_preserved()
    {
        var normalizer = new CarzinVehicleCatalogItemNormalizer(TaxonomyReader.Valid());
        await normalizer.PrepareAsync(TestContext.Current.CancellationToken);

        var absent = normalizer.Normalize(Source() with
        {
            VersionCode = null, Color = null, Transmission = null, Fuel = null
        });
        var newProviderValues = normalizer.Normalize(Source() with
        {
            Color = "Azul Lunar", Transmission = "Automatizado", Fuel = "Hidrogênio"
        });

        Assert.True(absent.IsValid);
        Assert.Null(absent.Vehicle!.Version);
        Assert.Null(absent.Vehicle.Color);
        Assert.Null(absent.Vehicle.Transmission);
        Assert.Null(absent.Vehicle.Fuel);
        Assert.True(newProviderValues.IsValid);
        Assert.Equal("Azul Lunar", newProviderValues.Vehicle!.Color);
        Assert.Equal("Automatizado", newProviderValues.Vehicle.Transmission);
        Assert.Equal("Hidrogênio", newProviderValues.Vehicle.Fuel);
    }

    [Fact]
    public async Task Empty_or_incomplete_taxonomy_fails_preparation_with_stable_sanitized_reason()
    {
        var reader = new TaxonomyReader { Snapshot = new([], [], []) };
        var normalizer = new CarzinVehicleCatalogItemNormalizer(reader);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => normalizer.PrepareAsync(TestContext.Current.CancellationToken));

        Assert.Equal("carzin_taxonomy_incomplete", exception.Message);
    }

    [Fact]
    public async Task Blank_or_trimmed_duplicate_taxonomy_keys_fail_with_stable_corruption_reason()
    {
        var reader = TaxonomyReader.Valid();
        reader.Snapshot = reader.Snapshot with
        {
            Manufacturers =
            [
                .. reader.Snapshot.Manufacturers,
                new(Guid.NewGuid(), " 1 ", "Duplicada")
            ]
        };
        var normalizer = new CarzinVehicleCatalogItemNormalizer(reader);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => normalizer.PrepareAsync(TestContext.Current.CancellationToken));

        Assert.Equal("carzin_taxonomy_corrupt", exception.Message);
        Assert.DoesNotContain("Duplicada", exception.Message);
    }

    [Fact]
    public async Task Taxonomy_row_with_parent_outside_snapshot_fails_as_corrupt()
    {
        var reader = TaxonomyReader.Valid();
        reader.Snapshot = reader.Snapshot with
        {
            Models =
            [
                .. reader.Snapshot.Models,
                new(Guid.NewGuid(), Guid.NewGuid(), "99", "Orphan", "99")
            ]
        };
        var normalizer = new CarzinVehicleCatalogItemNormalizer(reader);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => normalizer.PrepareAsync(TestContext.Current.CancellationToken));

        Assert.Equal("carzin_taxonomy_corrupt", exception.Message);
    }

    [Fact]
    public void Normalization_result_factories_enforce_valid_and_invalid_invariants()
    {
        var source = Source();

        var valid = CatalogItemNormalizationResult.Valid(source);
        var invalid = CatalogItemNormalizationResult.Invalid("model_code_unknown");

        Assert.True(valid.IsValid);
        Assert.Same(source, valid.Vehicle);
        Assert.Null(valid.Reason);
        Assert.False(invalid.IsValid);
        Assert.Null(invalid.Vehicle);
        Assert.Equal("model_code_unknown", invalid.Reason);
        Assert.Throws<ArgumentException>(() => CatalogItemNormalizationResult.Invalid("code=secret"));
    }

    private static CatalogSourceVehicle Source() => new(
        "1", "Civic", null, null, null, " 1 ", "2", "3", 2024, 10, 4, false,
        "Gasolina e Álcool", "Manual", "Prata", 100_000, "São Paulo", "SP", "url", "image");

    private sealed class TaxonomyReader : ICarzinVehicleCatalogTaxonomyReader
    {
        public int Reads { get; private set; }
        public int QueryCount { get; private set; }
        public (int, int, int) Calls => Reads == 0 ? (0, 0, 0) : (1, 1, 1);
        public CarzinVehicleCatalogTaxonomySnapshot Snapshot { get; set; } = CreateValidSnapshot();

        public static TaxonomyReader Valid() => new();

        public Task<CarzinVehicleCatalogTaxonomySnapshot> ReadSnapshotAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Reads++;
            QueryCount += 3;
            return Task.FromResult(Snapshot);
        }

        private static CarzinVehicleCatalogTaxonomySnapshot CreateValidSnapshot()
            => new(
                [new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "1", " Honda ")],
                [new(Guid.Parse("22222222-2222-2222-2222-222222222222"), Guid.Parse("11111111-1111-1111-1111-111111111111"), "2", " Civic ", "1")],
                [new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "3", " EXL ", "2", "1")]);
    }
}
