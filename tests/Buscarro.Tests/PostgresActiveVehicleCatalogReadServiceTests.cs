using Buscarro.Infrastructure.Repositories;

namespace Buscarro.Tests;

public sealed class PostgresActiveVehicleCatalogReadServiceTests
{
    [Fact]
    public void Bounded_count_conversion_rejects_postgresql_bigint_overflow()
    {
        Assert.Equal(int.MaxValue, PostgresVehicleCatalogReadService.CheckedCount(int.MaxValue));
        Assert.Throws<OverflowException>(() => PostgresVehicleCatalogReadService.CheckedCount((long)int.MaxValue + 1));
    }

    public static TheoryData<int?, int?, decimal?> IncompleteRequiredValues => new()
    {
        { null, 10, 100m },
        { 2020, null, 100m },
        { 2020, 10, null }
    };

    [Theory]
    [MemberData(nameof(IncompleteRequiredValues))]
    public void Incomplete_required_numeric_values_do_not_create_a_favorite_snapshot(
        int? year,
        int? mileage,
        decimal? price)
    {
        var row = new ActiveVehicleCatalogRow(
            "42", "Provider", "Title", null, null, null,
            year, mileage, null, null, null, null, null, price,
            null, null, null, null);

        var vehicle = PostgresActiveVehicleCatalogReadService.CreateVehicle(row);

        Assert.Null(vehicle);
    }
}
