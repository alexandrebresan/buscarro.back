using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Services;

namespace Buscarro.Tests;

public sealed class VehicleCatalogSearchCriteriaNormalizerTests
{
    private readonly VehicleCatalogSearchCriteriaNormalizer _sut=new();

    [Fact]
    public void Normalize_canonicalizes_deduplicates_and_orders_repeated_values()
    {
        var result=_sut.Normalize(new VehicleCatalogSearchRequest { Manufacturer=["  Fíat  ","fiat","Honda"],Color=[" ","Bránco  Pérola"] });
        Assert.Equal(["fiat","honda"],result.Manufacturers);
        Assert.Equal(["branco perola"],result.Colors);
        Assert.Equal(1,result.Page); Assert.Equal(20,result.PageSize);
    }

    [Fact]
    public void Normalize_accepts_matching_results_alias_and_rejects_conflict()
    {
        Assert.Equal(30,_sut.Normalize(new VehicleCatalogSearchRequest { PageSize=30,Results=30 }).PageSize);
        Assert.Throws<ValidationException>(()=>_sut.Normalize(new VehicleCatalogSearchRequest { PageSize=20,Results=30 }));
    }

    [Theory]
    [InlineData(0,20,null)] [InlineData(1,0,null)] [InlineData(1,51,null)]
    [InlineData(1,20,"rand")] [InlineData(1,20,"relevance")]
    public void Normalize_rejects_invalid_paging_and_order(int page,int pageSize,string? order)
        => Assert.Throws<ValidationException>(()=>_sut.Normalize(new VehicleCatalogSearchRequest { Page=page,PageSize=pageSize,Order=order }));

    [Theory]
    [InlineData("price")]
    [InlineData("price-inverse")]
    [InlineData("year")]
    [InlineData("year-inverse")]
    [InlineData("mileage")]
    [InlineData("mileage-inverse")]
    public void Normalize_accepts_supported_orders(string order)
    {
        var result = _sut.Normalize(
            new VehicleCatalogSearchRequest
            {
                Order = order
            });

        Assert.Equal(order, result.Order);
    }

    [Fact]
    public void Normalize_rejects_more_than_twenty_raw_values_even_when_duplicates()
        => Assert.Throws<ValidationException>(()=>_sut.Normalize(new VehicleCatalogSearchRequest { City=Enumerable.Repeat("Sao Paulo",21).ToList() }));

    [Fact]
    public void Normalize_rejects_invalid_ranges_and_future_year()
    {
        Assert.Throws<ValidationException>(()=>_sut.Normalize(new VehicleCatalogSearchRequest { PriceMin=2,PriceMax=1 }));
        Assert.Throws<ValidationException>(()=>_sut.Normalize(new VehicleCatalogSearchRequest { YearMax=DateTime.UtcNow.Year+2 }));
    }

    [Theory]
    [InlineData("city")]
    [InlineData("color")]
    [InlineData("fuel")]
    [InlineData("gear")]
    public void Normalize_rejects_numeric_provider_codes(string category)
    {
        var request = new VehicleCatalogSearchRequest();

        if (category == "city")
        {
            request.City = ["9668"];
        }
        else if (category == "color")
        {
            request.Color = ["4"];
        }
        else if (category == "fuel")
        {
            request.Fuel = ["2"];
        }
        else
        {
            request.Gear = ["1"];
        }

        Assert.Throws<ValidationException>(
            () => _sut.Normalize(request));
    }

    [Fact]
    public void Normalize_accepts_city_name()
    {
        var result = _sut.Normalize(
            new VehicleCatalogSearchRequest
            {
                City = ["  São   Paulo  "]
            });

        Assert.Equal(["sao paulo"], result.Cities);
    }
}
