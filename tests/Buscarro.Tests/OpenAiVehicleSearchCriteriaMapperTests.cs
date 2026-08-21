using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Services;
using Buscarro.Infrastructure.Contracts;
using Buscarro.Infrastructure.Services;

namespace Buscarro.Tests;

public sealed class OpenAiVehicleSearchCriteriaMapperTests
{
    private readonly OpenAiVehicleSearchCriteriaMapper _sut=new(new VehicleSearchCriteriaCanonicalizer());


    [Fact]
    public void Map_maps_plural_contract_explicitly()
    {
        var result = _sut.Map(
            new OpenAiVehicleSearchCriteriaContract
            {
                Manufacturers = ["Fiat", "Honda"],
                Cities = ["São Paulo"],
                Colors = ["branco", "cinza"]
            },
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(
            ["Fiat", "Honda"],
            result.Manufacturers);

        Assert.Equal(
            ["São Paulo"],
            result.Cities);

        Assert.Equal(
            ["branco", "cinza"],
            result.Colors);
    }

    [Theory]
    [InlineData("providers")] [InlineData("states")] [InlineData("cities")] [InlineData("manufacturers")]
    [InlineData("colors")] [InlineData("gears")] [InlineData("fuels")]
    public void Map_rejects_numeric_values_in_any_plural_array(string category)
    {
        var contract=new OpenAiVehicleSearchCriteriaContract();
        typeof(OpenAiVehicleSearchCriteriaContract).GetProperty(char.ToUpperInvariant(category[0])+category[1..])!.SetValue(contract,new List<string>{"21"});
        Assert.Throws<ValidationException>(() => _sut.Map(
            contract,
            TestVehicleSearchTaxonomy.Default));
    }

    [Fact]
    public void Map_allows_numeric_model_and_version_names_without_inventing_ids()
    {
        var result = _sut.Map(
            new OpenAiVehicleSearchCriteriaContract
            {
                Models = ["208", "500"],
                Versions = ["2008", "911"]
            },
            TestVehicleSearchTaxonomy.Default);

        Assert.Equal(["208","500"],result.Models);Assert.Equal(["2008","911"],result.Versions);
    }

    [Fact]
    public void Map_uses_the_taxonomy_received_by_parameter()
    {
        var taxonomy = new VehicleSearchTaxonomy(
            Colors: ["azul lunar"],
            Gears: ["manual"],
            Fuels: ["elétrico"]);

        var contract =
            new OpenAiVehicleSearchCriteriaContract
            {
                Colors = ["AZUL LUNAR"]
            };

        var result = _sut.Map(
            contract,
            taxonomy);

        Assert.Equal(
            ["azul lunar"],
            result.Colors);
    }
}
