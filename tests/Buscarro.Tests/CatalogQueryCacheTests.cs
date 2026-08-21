using Buscarro.Application.DTOs;
using Buscarro.Infrastructure.Services;

namespace Buscarro.Tests;

public sealed class CatalogQueryCacheTests
{
    [Fact]
    public void Count_key_excludes_order_and_paging_but_page_key_includes_them()
    {
        var first=Criteria() with { Order="price",Page=1,PageSize=10 };
        var second=Criteria() with { Order="year",Page=2,PageSize=20 };
        Assert.Equal(CatalogQueryCache.BuildCountKey(7,first),CatalogQueryCache.BuildCountKey(7,second));
        Assert.NotEqual(CatalogQueryCache.BuildPageKey(7,first),CatalogQueryCache.BuildPageKey(7,second));
        Assert.NotEqual(CatalogQueryCache.BuildCountKey(7,first),CatalogQueryCache.BuildCountKey(8,first));
    }
    private static NormalizedVehicleCatalogSearchCriteria Criteria()=>new(null,[],[],[],[],[],[],[],[],[],null,null,null,null,null,1,20);
}
