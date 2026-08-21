using Buscarro.Application.DTOs;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using Buscarro.Application.Services;

namespace Buscarro.Tests;

public sealed class FindAllVehiclesHandlerTests
{
    [Fact]
    public async Task Handler_normalizes_delegates_and_maps_exact_metadata_and_nulls()
    {
        var service=new FakeSearch { Result=new VehicleCatalogSearchResult { PublicationId=43,CatalogUpdatedAtUtc=DateTimeOffset.Parse("2026-08-15T03:00:00Z"),TotalCount=21,Page=2,PageSize=20,TotalPages=2,Vehicles=[new VehicleResponse { ExternalId="1",Provider="Carzin",Title="Uno",Brand=null }] } };
        var handler=new FindAllVehiclesHandler(service,new VehicleCatalogSearchCriteriaNormalizer());
        var response=await handler.Handle(new FindAllVehiclesQuery(new VehicleCatalogSearchRequest { Manufacturer=[" Fíat "],Page=2 }),CancellationToken.None);
        Assert.Equal(["fiat"],service.Criteria!.Manufacturers); Assert.Equal(43,response.PublicationId); Assert.Equal(21,response.TotalCount); Assert.Equal(1,response.Results); Assert.Null(response.Vehicles.Single().Brand);
        Assert.Null(typeof(FindAllVehiclesResponse).GetProperty("GenerationId"));
    }
    private sealed class FakeSearch : IVehicleSearchReadService
    {
        public VehicleCatalogSearchResult Result { get; init; }=new(); public NormalizedVehicleCatalogSearchCriteria? Criteria { get; private set; }
        public Task<VehicleCatalogSearchResult> SearchAsync(NormalizedVehicleCatalogSearchCriteria criteria,CancellationToken token) { Criteria=criteria; return Task.FromResult(Result); }
    }
}
