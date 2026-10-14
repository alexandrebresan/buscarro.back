using Buscarro.Api.Middlewares;
using Buscarro.Application.Exceptions;
using Buscarro.Application.DTOs;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Repositories;
using Buscarro.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Buscarro.Tests;

public sealed class ApiExceptionMiddlewareTests
{
    [Fact]
    public async Task Unknown_auth_commit_clears_cookie_and_returns_operational_error_without_sensitive_detail()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/auth/refresh";
        context.Response.Body = new MemoryStream();
        var middleware = new ApiExceptionMiddleware(_ => throw new TransactionCommitOutcomeUnknownException(new Exception("private-database-detail")), NullLogger<ApiExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Contains("session_state_unknown", body);
        Assert.DoesNotContain("private-database-detail", body);
        Assert.Contains("__Host-Buscarro.Refresh=", context.Response.Headers.SetCookie.ToString());
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Dependency_failure_does_not_masquerade_as_invalid_session()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ApiExceptionMiddleware(_ => throw new AuthUnavailableException(), NullLogger<ApiExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(503, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
    }
    [Theory]
    [InlineData(true,400,"validation_error")]
    [InlineData(false,503,"catalog_unavailable")]
    public async Task Middleware_maps_public_application_errors(bool validation,int status,string code)
    {
        var context=new DefaultHttpContext();context.Response.Body=new MemoryStream();
        var middleware=new ApiExceptionMiddleware(_=>throw (validation?(Exception)new ValidationException("invalid"):new CatalogUnavailableException()),NullLogger<ApiExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        context.Response.Body.Position=0;var body=await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(status,context.Response.StatusCode);Assert.Contains(code,body);Assert.DoesNotContain("stack",body,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Client_cancellation_is_not_converted_to_problem_details()
    {
        using var cts=new CancellationTokenSource();cts.Cancel();var context=new DefaultHttpContext();context.RequestAborted=cts.Token;
        var middleware=new ApiExceptionMiddleware(_=>throw new OperationCanceledException(),NullLogger<ApiExceptionMiddleware>.Instance);
        await Assert.ThrowsAsync<OperationCanceledException>(()=>middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task Conflict_is_safe_409_problem_details()
    {
        var context=new DefaultHttpContext();context.Response.Body=new MemoryStream();
        var middleware=new ApiExceptionMiddleware(_=>throw new ConflictException("Operacao concorrente."),NullLogger<ApiExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);context.Response.Body.Position=0;var body=await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(409,context.Response.StatusCode);Assert.Contains("conflict",body);Assert.DoesNotContain("stack",body,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transient_postgres_search_failure_is_a_safe_503_without_fallback()
    {
        await using var dataSource = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1;Pooling=false");
        var options = Options.Create(new CatalogQueryOptions { CommandTimeoutSeconds = 1 });
        using var cache = new CatalogQueryCache(options);
        var reader = new PostgresVehicleCatalogReadService(dataSource, cache, options);
        var criteria = new NormalizedVehicleCatalogSearchCriteria(
            null, [], [], [], [], [], [], [], [], [],
            null, null, null, null, null, 1, 20);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ApiExceptionMiddleware(
            async _ => await reader.SearchAsync(criteria, CancellationToken.None),
            NullLogger<ApiExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Contains("catalog_unavailable", body);
        Assert.DoesNotContain("127.0.0.1", body);
    }
}
