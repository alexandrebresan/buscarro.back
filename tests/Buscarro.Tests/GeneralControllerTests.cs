using Buscarro.Api.Controllers;
using Buscarro.Application.DTOs;
using Buscarro.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace Buscarro.Tests;

public sealed class GeneralControllerTests
{
    [Fact]
    public async Task Models_sends_optional_manufacturer_name_and_returns_names()
    {
        var mediator = new ThrowingMediator { Failure = null };
        var controller = new GeneralController(mediator, NullLogger<GeneralController>.Instance);
        using var cancellation = new CancellationTokenSource();
        var result = Assert.IsType<OkObjectResult>(await controller.ListModels("Jeep", cancellation.Token));
        Assert.Equal(new ListModelsQuery("Jeep"), mediator.LastRequest);
        Assert.Equal(cancellation.Token, mediator.LastToken);
        Assert.Equal(["Jeep"], Assert.IsAssignableFrom<IReadOnlyCollection<string>>(result.Value));
        await controller.ListModels(null, cancellation.Token);
        Assert.Equal(new ListModelsQuery(null), mediator.LastRequest);
    }

    [Fact]
    public async Task Versions_sends_model_and_optional_manufacturer_names()
    {
        var mediator = new ThrowingMediator { Failure = null };
        var controller = new GeneralController(mediator, NullLogger<GeneralController>.Instance);
        await controller.ListVersions("Wrangler", "Jeep", CancellationToken.None);
        Assert.Equal(new ListVersionsQuery("Wrangler", "Jeep"), mediator.LastRequest);
        await controller.ListVersions("Wrangler", null, CancellationToken.None);
        Assert.Equal(new ListVersionsQuery("Wrangler"), mediator.LastRequest);
    }

    [Fact]
    public async Task Versions_returns_bad_request_for_application_validation()
    {
        var mediator = new ThrowingMediator { Failure = new Buscarro.Application.Exceptions.ValidationException("model e obrigatorio.") };
        var controller = new GeneralController(mediator, NullLogger<GeneralController>.Instance);
        var result = Assert.IsType<BadRequestObjectResult>(await controller.ListVersions(null, null, CancellationToken.None));
        Assert.Equal("model e obrigatorio.", result.Value);
    }

    [Fact]
    public void GeneralController_exposes_public_catalog_endpoints()
    {
        var actionNames = typeof(GeneralController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(["ListManufacturers", "ListModels", "ListVersions"], actionNames);
        Assert.Empty(typeof(GeneralController).GetCustomAttributes<AuthorizeAttribute>());
    }

    [Fact]
    public async Task ListManufacturers_returns_service_unavailable_when_database_connection_fails()
    {
        var controller = new GeneralController(
            new ThrowingMediator(),
            NullLogger<GeneralController>.Instance);

        var result = await controller.ListManufacturers(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
        Assert.Equal("Banco de dados indisponivel.", objectResult.Value);
    }

    private sealed class ThrowingMediator : IMediator
    {
        public Exception? Failure { get; init; } = CreateDatabaseException();
        public object? LastRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            LastToken = cancellationToken;
            if (Failure is not null) throw Failure;
            return Task.FromResult((TResponse)(object)(IReadOnlyCollection<string>)["Jeep"]);
        }

        public Task Send<TRequest>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            throw CreateDatabaseException();
        }

        public Task<object?> Send(
            object request,
            CancellationToken cancellationToken = default)
        {
            throw CreateDatabaseException();
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

        private static RetryLimitExceededException CreateDatabaseException()
        {
            return new RetryLimitExceededException(
                "The maximum number of retries was exceeded.",
                new InvalidOperationException("Connection refused."));
        }
    }
}
