using Buscarro.Application.DTOs;
using Buscarro.Infrastructure.Repositories;

namespace Buscarro.Tests;

public sealed class VehicleCatalogSqlPredicateBuilderTests
{
    [Fact]
    public void Build_uses_names_only_with_or_inside_and_between_categories()
    {
        var criteria=Criteria() with { Manufacturers=["fiat","21"],Colors=["branco","cinza"],PriceMin=100m,YearMax=2025 };
        var result=new VehicleCatalogSqlPredicateBuilder().Build(criteria,[10,11],["Carzin"]);
        Assert.Contains("v.brand_normalized = ANY(@manufacturers)",result.WhereSql);
        Assert.DoesNotContain("manufacturer_code",result.WhereSql);
        Assert.Contains("AND v.color_normalized = ANY(@colors) AND",result.WhereSql);
        Assert.Contains("v.price >= @price_min",result.WhereSql);
        Assert.Contains("v.year <= @year_max",result.WhereSql);
    }

    [Theory]
    [InlineData(null,"v.id ASC")] [InlineData("price","v.price ASC NULLS LAST, v.id ASC")] [InlineData("year-inverse","v.year DESC NULLS LAST, v.id ASC")]
    public void OrderBy_is_closed_and_stable(string? order,string expected) => Assert.Equal(expected,VehicleCatalogSqlPredicateBuilder.OrderBy(order));

    private static NormalizedVehicleCatalogSearchCriteria Criteria()=>new(null,[],[],[],[],[],[],[],[],[],null,null,null,null,null,1,20);
}
