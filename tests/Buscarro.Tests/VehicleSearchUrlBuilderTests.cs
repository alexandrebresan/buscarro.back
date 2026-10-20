using System.Globalization;
using Buscarro.Application.DTOs;
using Buscarro.Application.Services;

namespace Buscarro.Tests;

public sealed class VehicleSearchUrlBuilderTests
{
    private readonly VehicleSearchUrlBuilder _builder = new();

    [Fact]
    public void Build_returns_base_url_when_no_search_filters_exist()
    {
        var result = _builder.Build(
            new VehicleSearchCriteria
            {
                Page = 5,
                Results = 50,
                Query = "ignorado"
            },
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal("/api/vehicles", result);
    }

    [Fact]
    public void Build_uses_the_approved_parameter_order()
    {
        var result = _builder.Build(
            new VehicleSearchCriteria
            {
                States = ["SP"],
                Cities = ["São Paulo"],
                Manufacturers = ["Fiat"],
                Models = ["Uno"],
                Versions = ["Way"],
                PriceMin = 10_000m,
                PriceMax = 65_000m,
                YearMin = 2014,
                YearMax = 2024,
                Colors = ["branco"],
                Gears = ["manual"],
                Fuels = ["gasolina e álcool"]
            },
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(
            "/api/vehicles?state=SP" +
            "&city=S%C3%A3o%20Paulo" +
            "&manufacturer=Fiat" +
            "&model=Uno" +
            "&version=Way" +
            "&priceMin=10000" +
            "&priceMax=65000" +
            "&yearMin=2014" +
            "&yearMax=2024" +
            "&color=branco" +
            "&gear=manual" +
            "&fuel=gasolina%20e%20%C3%A1lcool",
            result);
    }

    [Fact]
    public void Build_trims_strings_omits_whitespace_and_escapes_values()
    {
        var result = _builder.Build(
            new VehicleSearchCriteria
            {
                States = ["  São Paulo  "],
                Manufacturers = ["  Citroën & DS "],
                Models = ["   "],
                Gears = ["\t"]
            },
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(
            "/api/vehicles?state=S%C3%A3o%20Paulo" +
            "&manufacturer=Citro%C3%ABn%20%26%20DS",
            result);
    }

    [Fact]
    public void Build_formats_numbers_with_invariant_culture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture =
                CultureInfo.GetCultureInfo("pt-BR");

            var result = _builder.Build(
                new VehicleSearchCriteria
                {
                    PriceMin = 1234.56m
                },
                TestVehicleSearchTaxonomy.Default);

            Assert.Equal(
                "/api/vehicles?priceMin=1234.56",
                result);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Build_includes_order_and_excludes_pagination_and_query()
    {
        var result = _builder.Build(
            new VehicleSearchCriteria
            {
                Query = "uno",
                Order = "price",
                Page = 9,
                Results = 100,
                Colors = ["preto"]
            },
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal("/api/vehicles?order=price&color=preto", result);
    }

    [Fact]
    public void Build_emits_repeated_singular_parameter_names_in_canonical_order()
    {
        var result = _builder.Build(
            new VehicleSearchCriteria
            {
                Providers = ["Carzin", "OLX"],
                States = ["SP", "RJ"],
                Cities = ["São Paulo", "Rio"],
                Manufacturers = ["Fiat", "Honda"],
                Models = ["Uno", "Civic"],
                Versions = ["Way", "EXL"],
                Colors = ["branco", "cinza"],
                Gears = ["manual", "automático"],
                Fuels = ["gasolina", "elétrico"]
            },
            TestVehicleSearchTaxonomy.Default);
        Assert.Equal("/api/vehicles?provider=Carzin&provider=OLX&state=SP&state=RJ&city=S%C3%A3o%20Paulo&city=Rio&manufacturer=Fiat&manufacturer=Honda&model=Uno&model=Civic&version=Way&version=EXL&color=branco&color=cinza&gear=manual&gear=autom%C3%A1tico&fuel=gasolina&fuel=el%C3%A9trico",result);
    }

    [Fact]
    public void Build_uses_the_taxonomy_received_by_parameter()
    {
        var taxonomy = new VehicleSearchTaxonomy(
            Colors: ["azul lunar"],
            Gears: ["manual"],
            Fuels: ["elétrico"]);

        var result = _builder.Build(
            new VehicleSearchCriteria
            {
                Colors = ["AZUL LUNAR"]
            },
            taxonomy);

        Assert.Equal(
            "/api/vehicles?color=azul%20lunar",
            result);
    }
}
