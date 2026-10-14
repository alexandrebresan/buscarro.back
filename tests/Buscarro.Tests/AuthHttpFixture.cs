using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Buscarro.Api.Configurations;
using Buscarro.Api.Middlewares;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Buscarro.Tests;

internal sealed class AuthHttpFixture : IAsyncDisposable, IGoogleTokenValidator
{
    private const string Secret = "buscarro-http-test-only-key-at-least-32-bytes";
    public WebApplication App { get; private set; } = null!;
    private HttpClient _client = null!;
    private string? _csrf;
    public Dictionary<string, string> Cookies { get; } = [];
    public GoogleIdentity? GoogleIdentity
    {
        get; set;
    }

    public static async Task<AuthHttpFixture> StartAsync(string? postgresConnection = null, string? certificatePath = null)
    {
        var fixture = new AuthHttpFixture();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ApplicationName = typeof(ApiHostComposition).Assembly.GetName().Name
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:BuscarroDb"] = postgresConnection ?? "Host=localhost;Database=buscarro_http_test;Username=test",
            ["Auth:DataProtectionCertificatePath"] = certificatePath,
            ["Jwt:Issuer"] = "http-test",
            ["Jwt:Audience"] = "http-test",
            ["Jwt:Secret"] = Secret,
            ["Jwt:ExpirationMinutes"] = "15",
            ["Auth:AllowedOrigins:0"] = "https://buscarro.test",
            ["Google:ClientId"] = "test-client.apps.googleusercontent.com",
            ["OpenAI:BaseUrl"] = "https://api.openai.test/",
            ["OpenAI:Model"] = "test-model"
        });
        builder.WebHost.UseTestServer();
        ApiHostComposition.ConfigureServices(builder);
        if (postgresConnection is null)
        {
            builder.Services.RemoveAll<BuscarroDbContext>();
            builder.Services.RemoveAll<DbContextOptions<BuscarroDbContext>>();
            builder.Services.RemoveAll<IDbContextFactory<BuscarroDbContext>>();
            builder.Services.RemoveAll<IDbContextOptionsConfiguration<BuscarroDbContext>>();
            var database = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<BuscarroDbContext>(options => options.UseInMemoryDatabase(database), optionsLifetime: ServiceLifetime.Singleton);
            builder.Services.AddDbContextFactory<BuscarroDbContext>(options => options.UseInMemoryDatabase(database));
        }
        builder.Services.RemoveAll<IGoogleTokenValidator>();
        builder.Services.AddSingleton<IGoogleTokenValidator>(fixture);
        fixture.App = builder.Build();
        fixture.App.UseMiddleware<ApiExceptionMiddleware>();
        fixture.App.UseAuthentication();
        fixture.App.UseAuthorization();
        fixture.App.MapControllers();
        if (postgresConnection is not null)
        {
            await using var scope = fixture.App.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<BuscarroDbContext>().Database.MigrateAsync();
        }
        await fixture.App.StartAsync();
        fixture._client = fixture.App.GetTestClient();
        fixture._client.BaseAddress = new Uri("https://buscarro.test");
        return fixture;
    }

    public void CopyBrowserFrom(AuthHttpFixture other)
    {
        _csrf = other._csrf;
        Cookies.Clear();
        foreach (var cookie in other.Cookies)
            Cookies[cookie.Key] = cookie.Value;
    }

    public async Task BootstrapCsrfAsync()
    {
        using var response = await GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        _csrf = json.GetProperty("token").GetString();
    }

    public Task<HttpResponseMessage> GetAsync(string path, string? bearer = null)
        => SendAsync(new HttpRequestMessage(HttpMethod.Get, path), bearer);

    public Task<HttpResponseMessage> PostAsync(string path, object body, string? bearer = null, string? origin = "https://buscarro.test")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (_csrf is not null)
            request.Headers.Add("X-CSRF-Token", _csrf);
        if (origin is not null)
            request.Headers.Add("Origin", origin);
        return SendAsync(request, bearer);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string? bearer)
    {
        using (request)
        {
            if (Cookies.Count > 0)
                request.Headers.Add("Cookie", string.Join("; ", Cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));
            if (bearer is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            var response = await _client.SendAsync(request);
            if (response.Headers.TryGetValues("Set-Cookie", out var values))
                foreach (var header in values)
                {
                    var pair = header.Split(';')[0].Split('=', 2);
                    if (pair[1].Length == 0)
                        Cookies.Remove(pair[0]);
                    else
                        Cookies[pair[0]] = pair[1];
                }
            return response;
        }
    }

    public string CreateExpiredToken(AuthResponse response)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        var expired = new JwtSecurityToken("http-test", "http-test",
            jwt.Claims.Where(claim => claim.Type is "sub" or "sid" || claim.Type == ClaimTypes.NameIdentifier),
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(expired);
    }

    public Task<GoogleIdentity> ValidateAsync(string token, CancellationToken cancellationToken)
        => Task.FromResult(GoogleIdentity ?? throw new InvalidOperationException("Configure fake verified identity first."));

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await App.DisposeAsync();
    }
}
