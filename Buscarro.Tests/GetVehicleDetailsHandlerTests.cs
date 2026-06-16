using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;

namespace Buscarro.Tests;

public sealed class GetVehicleDetailsHandlerTests
{
    [Fact]
    public async Task Handle_returns_details_from_read_service()
    {
        var handler = new GetVehicleDetailsHandler(
            new FakeVehicleDetailsReadService(new VehicleDetailsResponse
            {
                Observations = ["IPVA Pago"],
                Optionals = ["Air bag"],
                Images =
                [
                    new VehicleDetailsImageResponse
                    {
                        ImageUrl = "http://url.com/2211-1.jpg",
                        ThumbUrl = "http://url.com/2211-1_thumb.jpg"
                    }
                ]
            }));

        var response = await handler.Handle(new GetVehicleDetailsQuery(29254), CancellationToken.None);

        Assert.Equal(["IPVA Pago"], response.Observations);
        Assert.Equal(["Air bag"], response.Optionals);
        var image = Assert.Single(response.Images);
        Assert.Equal("http://url.com/2211-1.jpg", image.ImageUrl);
        Assert.Equal("http://url.com/2211-1_thumb.jpg", image.ThumbUrl);
    }

    [Fact]
    public async Task Handle_throws_validation_exception_when_external_id_is_not_positive()
    {
        var handler = new GetVehicleDetailsHandler(new FakeVehicleDetailsReadService(new VehicleDetailsResponse()));

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new GetVehicleDetailsQuery(0), CancellationToken.None));

        Assert.Equal("ExternalId deve ser maior que zero.", exception.Message);
    }

    [Fact]
    public async Task Handle_throws_not_found_when_details_cannot_be_resolved()
    {
        var handler = new GetVehicleDetailsHandler(new FakeVehicleDetailsReadService(null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetVehicleDetailsQuery(404), CancellationToken.None));
    }

    private sealed class FakeVehicleDetailsReadService : IVehicleDetailsReadService
    {
        private readonly VehicleDetailsResponse? _response;

        public FakeVehicleDetailsReadService(VehicleDetailsResponse? response)
        {
            _response = response;
        }

        public Task<VehicleDetailsResponse?> GetVehicleDetailsAsync(
            int externalId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}
