using Buscarro.Application.DTOs;
using Buscarro.Application.Services;

namespace Buscarro.Tests;

public sealed class VehicleSearchCriteriaMergerTests
{
    [Fact]
    public void Merge_applies_semi_new_using_the_injected_year()
    {
        var merger = CreateMerger(new DateTimeOffset(
            2026, 12, 31, 23, 59, 59, TimeSpan.Zero));

        var result = merger.Merge(
            new VehicleSearchCriteria(),
            ["semi_new"],
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(2023, result.YearMin);
    }

    [Fact]
    public void Merge_recalculates_semi_new_after_the_utc_year_changes()
    {
        var merger = CreateMerger(new DateTimeOffset(
            2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var result = merger.Merge(
            new VehicleSearchCriteria(),
            ["semi_new"],
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(2024, result.YearMin);
    }

    [Fact]
    public void Merge_applies_all_beginner_fields()
    {
        var result = CreateMerger().Merge(
            new VehicleSearchCriteria(),
            ["beginner"],
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(["manual"], result.Gears);
        Assert.Equal(65_000m, result.PriceMax);
        Assert.Equal(["gasolina e álcool"], result.Fuels);
        Assert.Equal(2014, result.YearMin);
    }

    [Fact]
    public void Merge_keeps_the_first_profile_value_and_later_fills_free_fields()
    {
        var result = CreateMerger().Merge(
            new VehicleSearchCriteria(),
            ["semi_new", "beginner"],
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(2023, result.YearMin);
        Assert.Equal(["manual"], result.Gears);
        Assert.Equal(65_000m, result.PriceMax);
        Assert.Equal(["gasolina e álcool"], result.Fuels);
    }

    [Fact]
    public void Merge_respects_the_reverse_profile_order()
    {
        var result = CreateMerger().Merge(
            new VehicleSearchCriteria(),
            ["beginner", "semi_new"],
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(2014, result.YearMin);
        Assert.Equal(["manual"], result.Gears);
        Assert.Equal(65_000m, result.PriceMax);
        Assert.Equal(["gasolina e álcool"], result.Fuels);
    }

    [Fact]
    public void Merge_explicit_values_override_each_profile_field_independently()
    {
        var explicitCriteria = new VehicleSearchCriteria
        {
            Gears = ["automático"],
            PriceMax = 90_000m,
            Fuels = ["elétrico"],
            YearMin = 2020
        };

        var result = CreateMerger().Merge(
            explicitCriteria,
            ["beginner", "semi_new"],
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(["automático"], result.Gears);
        Assert.Equal(90_000m, result.PriceMax);
        Assert.Equal(["elétrico"], result.Fuels);
        Assert.Equal(2020, result.YearMin);
    }

    [Fact]
    public void Merge_explicit_nonempty_collection_wins_whole_category_and_profiles_fill_only_empty_categories()
    {
        var result = CreateMerger().Merge(
            new VehicleSearchCriteria
            {
                Gears = ["automático", "manual"],
                Colors = []
            },
            ["beginner"],
            TestVehicleSearchTaxonomy.Default);
        Assert.Equal(["automático","manual"],result.Gears);Assert.Equal(["gasolina e álcool"],result.Fuels);
    }

    [Fact]
    public void Merge_treats_whitespace_strings_as_empty()
    {
        var result = CreateMerger().Merge(
            new VehicleSearchCriteria
            {
                Gears =[ " "],
                Fuels = ["\t"]
            },
            ["beginner"],
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(["manual"], result.Gears);
        Assert.Equal(["gasolina e álcool"], result.Fuels);
    }

    [Fact]
    public void Merge_returns_a_clone_with_every_current_field()
    {
        var source = new VehicleSearchCriteria
        {
            Query = "hatch",
            Order = "price",
            Page = 7,
            Results = 42,
            States = ["SP"],
            Cities = ["São Paulo"],
            Manufacturers = ["Fiat"],
            Models = ["Uno"],
            Versions = ["Way"],
            PriceMin = 10_000.50m,
            PriceMax = 80_000.75m,
            YearMin = 2018,
            YearMax = 2024,
            Colors = ["branco"],
            Gears = ["manual"],
            Fuels = ["gasolina e álcool"]
        };

        var result = CreateMerger().Merge(
            source,
            [],
            TestVehicleSearchTaxonomy.Default);

        Assert.NotSame(source, result);
        Assert.Equal(source.Query, result.Query);
        Assert.Equal(source.Order, result.Order);
        Assert.Equal(source.Page, result.Page);
        Assert.Equal(source.Results, result.Results);
        Assert.Equal(source.States, result.States);
        Assert.Equal(source.Cities, result.Cities);
        Assert.Equal(source.Manufacturers, result.Manufacturers);
        Assert.Equal(source.Models, result.Models);
        Assert.Equal(source.Versions, result.Versions);
        Assert.Equal(source.PriceMin, result.PriceMin);
        Assert.Equal(source.PriceMax, result.PriceMax);
        Assert.Equal(source.YearMin, result.YearMin);
        Assert.Equal(source.YearMax, result.YearMax);
        Assert.Equal(source.Colors, result.Colors);
        Assert.Equal(source.Gears, result.Gears);
        Assert.Equal(source.Fuels, result.Fuels);
    }

    [Fact]
    public void Merger_field_policy_covers_every_public_criteria_property()
    {
        string[] mergeableFilters =
        [
            nameof(VehicleSearchCriteria.Providers),
            nameof(VehicleSearchCriteria.States),
            nameof(VehicleSearchCriteria.Cities),
            nameof(VehicleSearchCriteria.Manufacturers),
            nameof(VehicleSearchCriteria.Models),
            nameof(VehicleSearchCriteria.Versions),
            nameof(VehicleSearchCriteria.Colors),
            nameof(VehicleSearchCriteria.Gears),
            nameof(VehicleSearchCriteria.Fuels),
            nameof(VehicleSearchCriteria.Order),
            nameof(VehicleSearchCriteria.PriceMax),
            nameof(VehicleSearchCriteria.PriceMin),
            nameof(VehicleSearchCriteria.Query),
            nameof(VehicleSearchCriteria.YearMax),
            nameof(VehicleSearchCriteria.YearMin)
        ];
        string[] cloneOnlyPagination =
        [
            nameof(VehicleSearchCriteria.Page),
            nameof(VehicleSearchCriteria.Results)
        ];
        var expectedProperties = mergeableFilters
            .Concat(cloneOnlyPagination)
            .OrderBy(name => name)
            .ToArray();
        var actualProperties = typeof(VehicleSearchCriteria)
            .GetProperties()
            .Where(property => property.GetMethod?.IsPublic == true)
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(expectedProperties, actualProperties);
        Assert.Empty(mergeableFilters.Intersect(cloneOnlyPagination));
    }

    [Fact]
    public void Merge_never_mutates_the_source()
    {
        var source = new VehicleSearchCriteria();

        var result = CreateMerger().Merge(
            source,
            ["beginner"],
            TestVehicleSearchTaxonomy.Default);

        Assert.NotSame(
            source,
            result);

        Assert.Empty(source.Gears);
        Assert.Empty(source.Fuels);

        Assert.Null(source.PriceMax);
        Assert.Null(source.YearMin);

        Assert.Equal(
            ["manual"],
            result.Gears);

        Assert.Equal(
            ["gasolina e álcool"],
            result.Fuels);
    }

    [Fact]
    public void Merge_never_applies_page_or_results_from_profiles()
    {
        var catalog = new SemanticProfileCatalog(
        [
            new SemanticProfileDefinition(
                "pagination",
                ["paginação"],
                _ => new VehicleSearchCriteria
                {
                    Page = 99,
                    Results = 999,
                    States = ["RJ"]
                })
        ]);
        var merger = new VehicleSearchCriteriaMerger(
            catalog,
            TimeProvider.System);
        var source = new VehicleSearchCriteria
        {
            Page = 3,
            Results = 15
        };

        var result = merger.Merge(
            source,
            ["pagination"],
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(3, result.Page);
        Assert.Equal(15, result.Results);
        Assert.Equal(["RJ"], result.States);
    }

    [Fact]
    public void Merge_supports_catalog_profiles_without_a_profile_specific_switch()
    {
        var catalog = new SemanticProfileCatalog(
        [
            new SemanticProfileDefinition(
                "custom",
                ["customizado"],
                _ => new VehicleSearchCriteria
                {
                    States = ["MG"],
                    Cities = ["Belo Horizonte"],
                    Manufacturers = ["Honda"],
                    PriceMin = 25_000m,
                    Colors = ["prata"]
                })
        ]);
        var merger = new VehicleSearchCriteriaMerger(
            catalog,
            TimeProvider.System);

        var result = merger.Merge(
            new VehicleSearchCriteria(),
            ["custom"],
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(["MG"], result.States);
        Assert.Equal(["Belo Horizonte"], result.Cities);
        Assert.Equal(["Honda"], result.Manufacturers);
        Assert.Equal(25_000m, result.PriceMin);
        Assert.Equal(["prata"], result.Colors);
    }

    [Fact]
    public void Merge_throws_for_an_unknown_profile_code()
    {
        var merger = CreateMerger();

        Assert.Throws<KeyNotFoundException>(() =>
            merger.Merge(
                new VehicleSearchCriteria(),
                ["unknown"],
                TestVehicleSearchTaxonomy.Default));
    }

    private static VehicleSearchCriteriaMerger CreateMerger(
        DateTimeOffset? utcNow = null)
    {
        return new VehicleSearchCriteriaMerger(
            new SemanticProfileCatalog(),
            new FixedTimeProvider(
                utcNow ??
                new DateTimeOffset(
                    2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    [Fact]
    public void Merge_uses_the_taxonomy_received_by_parameter()
    {
        var taxonomy = new VehicleSearchTaxonomy(
            Colors: ["azul lunar"],
            Gears: ["manual"],
            Fuels: ["elétrico"]);

        var result = CreateMerger().Merge(
            new VehicleSearchCriteria
            {
                Colors = ["AZUL LUNAR"]
            },
            [],
            taxonomy);

        Assert.Equal(
            ["azul lunar"],
            result.Colors);
    }
}
