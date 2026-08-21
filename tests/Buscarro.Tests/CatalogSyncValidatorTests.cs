using Buscarro.Application.DTOs;
using Buscarro.Application.Services;

namespace Buscarro.Tests;

public sealed class CatalogSyncValidatorTests
{
    private readonly CatalogSyncValidator _validator = new(TimeProvider.System, new CatalogSynchronizationOptions());

    [Fact]
    public void Validate_and_normalize_accepts_valid_vehicle_without_inventing_values()
    {
        var source = new CatalogSourceVehicle("42", "  Honda   Civic  ", "Honda", "Civic", null,
            "1", "2", null, 2024, 10_000, 4, false, "Flex", "Automatico", "Prata",
            120_000m, "Sao Paulo", "SP", "https://example.test/42", "invalid-url");

        var result = _validator.ValidateAndNormalize(source);

        Assert.True(result.IsValid);
        Assert.Equal("Honda Civic", result.Vehicle!.Title);
        Assert.Equal("honda civic", CatalogSyncValidator.Normalize(result.Vehicle.Title));
        Assert.Null(result.Vehicle.ImageUrl);
        Assert.Null(result.Vehicle.Version);
    }

    [Theory]
    [InlineData(null, "missing_external_id")]
    [InlineData("   ", "missing_external_id")]
    public void Validate_rejects_only_missing_generic_external_id(string? id, string reason)
    {
        var source = new CatalogSourceVehicle(id, "Carro", null, null, null, null, null, null,
            2020, 0, null, null, null, null, null, 1, null, null, null, null);

        var result = _validator.ValidateAndNormalize(source);

        Assert.False(result.IsValid);
        Assert.Equal(reason, result.Reason);
    }

    [Theory]
    [InlineData("7e0e93fd-8f06-47f1-a579-d1a2702be98d")]
    [InlineData("provider:opaque/id")]
    [InlineData("0")]
    public void Generic_source_accepts_nonblank_opaque_external_ids(string id)
    {
        var source = new CatalogSourceVehicle(id, "Carro", null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null);
        var result = _validator.ValidateAndNormalize(source);
        Assert.True(result.IsValid);
        Assert.Equal(id, result.Vehicle!.ExternalId);
    }

    [Fact]
    public void Snapshot_rejects_more_than_fifty_percent_drop_but_accepts_exactly_fifty()
    {
        Assert.Null(_validator.ValidateSnapshot(50, 100));
        Assert.Equal("catalog_drop_exceeded", _validator.ValidateSnapshot(49, 100));
    }

    [Fact]
    public void Missing_numeric_values_are_valid_and_remain_absent()
    {
        var source = new CatalogSourceVehicle("1", "Carro", null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null);
        var result = _validator.ValidateAndNormalize(source);
        Assert.True(result.IsValid);
        Assert.Null(result.Vehicle!.Year);
        Assert.Null(result.Vehicle.Mileage);
        Assert.Null(result.Vehicle.Price);
    }

    [Fact]
    public void Positive_mileage_and_price_do_not_have_arbitrary_maximum()
    {
        var source = new CatalogSourceVehicle(
            "1",
            "Carro",
            null,
            null,
            null,
            null,
            null,
            null,
            2024,
            6_000_000,
            4,
            false,
            null,
            null,
            null,
            1_500_000_000m,
            null,
            null,
            null,
            null);

        var result = _validator.ValidateAndNormalize(source);

        Assert.True(result.IsValid);
        Assert.Equal(6_000_000, result.Vehicle!.Mileage);
        Assert.Equal(1_500_000_000m, result.Vehicle.Price);
    }
}
