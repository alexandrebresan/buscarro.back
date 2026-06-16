using Buscarro.Api.Controllers;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace Buscarro.Tests;

public sealed class VehiclesControllerTests
{
    [Fact]
    public void VehiclesController_exposes_only_vehicle_search_endpoint()
    {
        var actionNames = typeof(VehiclesController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(["FindAll", "GetDetails"], actionNames);
    }

    [Fact]
    public async Task GetDetails_sends_external_id_to_mediator_query()
    {
        var mediator = new CaptureMediator(new VehicleDetailsResponse
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
        });
        var controller = new VehiclesController(mediator, NullLogger<VehiclesController>.Instance);

        var result = await controller.GetDetails(29254, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<VehicleDetailsResponse>(okResult.Value);
        Assert.Equal(["IPVA Pago"], response.Observations);
        var query = Assert.IsType<GetVehicleDetailsQuery>(mediator.Request);
        Assert.Equal(29254, query.ExternalId);
    }

    [Fact]
    public async Task GetDetails_returns_bad_gateway_when_provider_access_is_denied()
    {
        var controller = new VehiclesController(
            new ThrowingMediator(new ProviderAccessDeniedException(
                "Carzin",
                "Nao temos permissao na Carzin para acessar os detalhes deste classificado.")),
            NullLogger<VehiclesController>.Instance);

        var result = await controller.GetDetails(371903, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, objectResult.StatusCode);
        Assert.Equal(
            "Nao temos permissao na Carzin para acessar os detalhes deste classificado.",
            objectResult.Value);
    }

    [Fact]
    public void GetDetails_route_accepts_only_integer_external_id()
    {
        var route = typeof(VehiclesController)
            .GetMethod(nameof(VehiclesController.GetDetails))!
            .GetCustomAttributes<HttpGetAttribute>()
            .Single();

        Assert.Equal("details/{externalId:int}", route.Template);
    }

    private sealed class CaptureMediator : IMediator
    {
        private readonly VehicleDetailsResponse _response;

        public CaptureMediator(VehicleDetailsResponse response)
        {
            _response = response;
        }

        public object? Request { get; private set; }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult((TResponse)(object)_response);
        }

        public Task Send<TRequest>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            Request = request;
            return Task.CompletedTask;
        }

        public Task<object?> Send(
            object request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult<object?>(_response);
        }

        public Task Publish(
            object notification,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            return EmptyStream<TResponse>();
        }

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default)
        {
            return EmptyStream<object?>();
        }

        private static async IAsyncEnumerable<T> EmptyStream<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class ThrowingMediator : IMediator
    {
        private readonly Exception _exception;

        public ThrowingMediator(Exception exception)
        {
            _exception = exception;
        }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public Task Send<TRequest>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            throw _exception;
        }

        public Task<object?> Send(
            object request,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public Task Publish(
            object notification,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            return EmptyStream<TResponse>();
        }

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default)
        {
            return EmptyStream<object?>();
        }

        private static async IAsyncEnumerable<T> EmptyStream<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
