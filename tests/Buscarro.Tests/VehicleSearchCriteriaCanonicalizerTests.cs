using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Services;

namespace Buscarro.Tests;

public sealed class VehicleSearchCriteriaCanonicalizerTests
{
    private readonly VehicleSearchCriteriaCanonicalizer _sut=new();

    [Fact]
    public void Canonicalize_cleans_deduplicates_and_does_not_mutate_source()
    {
        var source = new VehicleSearchCriteria
        {
            Manufacturers = [" Honda ", "fiat", "FIAT"],
            States = [" SP "],
            Cities = ["  São   Paulo  "]
        };

        var result = _sut.Canonicalize(source,TestVehicleSearchTaxonomy.Default);

        Assert.Equal(["Honda", "fiat"], result.Manufacturers);
        Assert.Equal(["SP"], result.States);
        Assert.Equal(["São Paulo"], result.Cities);

        Assert.Equal(
            [" Honda ", "fiat", "FIAT"],
            source.Manufacturers);

        Assert.NotSame(
            source.Manufacturers,
            result.Manufacturers);
    }

    [Fact]
    public void Canonicalize_preserves_city_name_and_maps_canonical_enums()
    {
        var result = _sut.Canonicalize(
            new VehicleSearchCriteria
            {
                Cities = [" São   Paulo "],
                Colors = [" BRÂNCO "],
                Gears = ["AUTOMÁTICO"],
                Fuels = ["ALCOOL"]
            },
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(["São Paulo"], result.Cities);
        Assert.Equal(["branco"], result.Colors);
        Assert.Equal(["automático"], result.Gears);
        Assert.Equal(["álcool"], result.Fuels);
    }

    [Fact]
    public void Canonicalize_rejects_invalid_enum_and_raw_or_combined_limit()
    {
        Assert.Throws<ValidationException>(() => _sut.Canonicalize(
            new VehicleSearchCriteria { Colors = ["azul espacial"] },
            TestVehicleSearchTaxonomy.Default));

        Assert.Throws<ValidationException>(() => _sut.Canonicalize(
            new VehicleSearchCriteria
            {
                Models = Enumerable.Repeat("x", 21).ToList()
            },
            TestVehicleSearchTaxonomy.Default));
    }

    [Theory]
    [InlineData("color")]
    [InlineData("gear")]
    [InlineData("fuel")]
    public void Canonicalize_rejects_numeric_enum_values(
        string category)
    {
        var criteria = new VehicleSearchCriteria();

        if (category == "color")
        {
            criteria.Colors = ["7"];
        }
        else if (category == "gear")
        {
            criteria.Gears = ["4"];
        }
        else
        {
            criteria.Fuels = ["2"];
        }

        Assert.Throws<ValidationException>(
            () => _sut.Canonicalize(
                criteria,
                TestVehicleSearchTaxonomy.Default));
    }

    [Fact]
    public void Canonicalize_rejects_invalid_price_and_year_ranges_using_utc_year()
    {
        var sut=new VehicleSearchCriteriaCanonicalizer(new FixedTimeProvider(new DateTimeOffset(2026,12,31,23,59,59,TimeSpan.Zero)));
        Assert.Throws<ValidationException>(() => sut.Canonicalize(
            new VehicleSearchCriteria { PriceMin = -1 },
            TestVehicleSearchTaxonomy.Default));

        Assert.Throws<ValidationException>(() => sut.Canonicalize(
            new VehicleSearchCriteria { PriceMin = 2, PriceMax = 1 },
            TestVehicleSearchTaxonomy.Default));

        Assert.Throws<ValidationException>(() => sut.Canonicalize(
            new VehicleSearchCriteria { YearMin = -1 },
            TestVehicleSearchTaxonomy.Default));

        Assert.Throws<ValidationException>(() => sut.Canonicalize(
            new VehicleSearchCriteria { YearMin = 2025, YearMax = 2024 },
            TestVehicleSearchTaxonomy.Default));

        Assert.Throws<ValidationException>(() => sut.Canonicalize(
            new VehicleSearchCriteria { YearMax = 2028 },
            TestVehicleSearchTaxonomy.Default));

        Assert.Equal(
            2027,
            sut.Canonicalize(
                new VehicleSearchCriteria { YearMax = 2027 },
                TestVehicleSearchTaxonomy.Default).YearMax);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now):TimeProvider
    { public override DateTimeOffset GetUtcNow()=>now; }

    [Fact]
    public void Canonicalize_uses_the_taxonomy_received_by_parameter()
    {
        var taxonomy = new VehicleSearchTaxonomy(
            Colors: ["azul lunar"],
            Gears: ["manual"],
            Fuels: ["elétrico"]);

        var source = new VehicleSearchCriteria
        {
            Colors = ["AZUL LUNAR"]
        };

        var result = _sut.Canonicalize(source, taxonomy);

        Assert.Equal(["azul lunar"], result.Colors);
    }
}
