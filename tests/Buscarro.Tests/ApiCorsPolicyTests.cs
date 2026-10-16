using Buscarro.Api.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Buscarro.Tests;

public sealed class ApiCorsPolicyTests
{
    private const string FrontendOrigin = "https://auto-match-wine.vercel.app";

    [Theory]
    [InlineData(FrontendOrigin, "GET", true)]
    [InlineData(FrontendOrigin, "OPTIONS", true)]
    [InlineData("https://untrusted.example", "GET", false)]
    [InlineData("https://untrusted.example", "OPTIONS", false)]
    [InlineData("https://auto-match-wine.vercel.app.untrusted.example", "GET", false)]
    [InlineData("https://auto-match-wine.vercel.app.untrusted.example", "OPTIONS", false)]
    public async Task Cors_allows_credentials_only_for_configured_origins(string origin, string method, bool allowed)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(ApiHostComposition).Assembly.GetName().Name
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:BuscarroDb"] = "Host=localhost;Database=unused;Username=unused",
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
            ["Jwt:Secret"] = "a-test-secret-with-at-least-32-bytes",
            ["Auth:AllowedOrigins:0"] = FrontendOrigin,
            ["Cors:AllowedOrigins:0"] = FrontendOrigin,
            ["OpenAI:BaseUrl"] = "https://api.openai.test/",
            ["OpenAI:Model"] = "test-model"
        });
        ApiHostComposition.ConfigureServices(builder);
        using var app = builder.Build();
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Headers.Origin = origin;
        if (method == "OPTIONS")
        {
            context.Request.Headers.AccessControlRequestMethod = "POST";
            context.Request.Headers.AccessControlRequestHeaders = "content-type,x-csrf-token";
        }

        var policy = await app.Services.GetRequiredService<ICorsPolicyProvider>()
            .GetPolicyAsync(context, ApiHostComposition.CorsPolicyName);
        Assert.NotNull(policy);
        var service = app.Services.GetRequiredService<ICorsService>();
        var result = service.EvaluatePolicy(context, policy);
        service.ApplyResult(result, context.Response);

        if (allowed)
        {
            Assert.Equal(FrontendOrigin, context.Response.Headers.AccessControlAllowOrigin.ToString());
            Assert.Equal("true", context.Response.Headers.AccessControlAllowCredentials.ToString());
            if (method == "OPTIONS")
            {
                Assert.Contains("POST", context.Response.Headers.AccessControlAllowMethods.ToString());
                Assert.Contains("x-csrf-token", context.Response.Headers.AccessControlAllowHeaders.ToString());
            }
        }
        else
        {
            Assert.False(result.IsOriginAllowed);
            Assert.Empty(context.Response.Headers.AccessControlAllowOrigin.ToString());
            Assert.Empty(context.Response.Headers.AccessControlAllowCredentials.ToString());
        }
    }
}
