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
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw CreateDatabaseException();
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
