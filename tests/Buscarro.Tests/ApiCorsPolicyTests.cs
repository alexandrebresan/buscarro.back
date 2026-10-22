using Buscarro.Api.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Buscarro.Tests;

public sealed class ApiCorsPolicyTests
{
    private const string FrontendOrigin = "https://www.buscarro.app";
    private const string LegacyFrontendOrigin = "https://auto-match-wine.vercel.app";

    [Theory]
    [InlineData(FrontendOrigin, "GET", true)]
    [InlineData(FrontendOrigin, "OPTIONS", true)]
    [InlineData(LegacyFrontendOrigin, "GET", false)]
    [InlineData(LegacyFrontendOrigin, "OPTIONS", false)]
    [InlineData("https://www.buscarro.app.untrusted.example", "GET", false)]
    [InlineData("https://www.buscarro.app.untrusted.example", "OPTIONS", false)]
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
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Buscarro.sln")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        builder.Configuration.AddJsonFile(Path.Combine(root, "src", "Buscarro.Api", "appsettings.json"));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:BuscarroDb"] = "Host=localhost;Database=unused;Username=unused",
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
            ["Jwt:Secret"] = "a-test-secret-with-at-least-32-bytes",
            ["OpenAI:BaseUrl"] = "https://api.openai.test/",
            ["OpenAI:Model"] = "test-model"
        });
        ApiHostComposition.ConfigureServices(builder);
        using var app = builder.Build();
        var authOrigins = app.Services.GetRequiredService<IOptions<AuthTransportOptions>>().Value.AllowedOrigins;
        Assert.Equal([FrontendOrigin], authOrigins);
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
            Assert.Equal(origin, context.Response.Headers.AccessControlAllowOrigin.ToString());
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
