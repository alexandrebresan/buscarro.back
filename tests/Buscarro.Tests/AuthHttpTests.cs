using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Buscarro.Application.DTOs;
using Buscarro.Domain.Entities;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Buscarro.Tests;

public sealed class AuthHttpTests
{
    [Fact]
    public async Task Csrf_endpoint_emits_cross_site_host_only_secure_cookie()
    {
        await using var host = await AuthHttpFixture.StartAsync();
        using var response = await host.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var header = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-Buscarro.Csrf="));
        var cookie = Microsoft.Net.Http.Headers.SetCookieHeaderValue.Parse(header);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.None, cookie.SameSite);
        Assert.True(cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.Equal("/", cookie.Path.ToString());
        Assert.False(cookie.Domain.HasValue);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));

        // The cookie alone never authorizes a POST without the request token.
        using var withoutToken = await host.PostAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.Forbidden, withoutToken.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Auth_posts_still_reject_missing_or_tampered_csrf_cookie(bool tampered)
    {
        await using var host = await AuthHttpFixture.StartAsync();
        await host.BootstrapCsrfAsync();
        if (tampered) host.Cookies["__Host-Buscarro.Csrf"] = "invalid-cookie";
        else host.Cookies.Remove("__Host-Buscarro.Csrf");
        using var response = await host.PostAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("csrf_invalid", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Registration_refresh_and_logout_preserve_favorites_and_revoke_access()
    {
        await using var host = await AuthHttpFixture.StartAsync();
        await host.BootstrapCsrfAsync();
        using var registration = await host.PostAsync("/api/auth/register", new
        {
            name = "Alex",
            email = "alex@example.com",
            password = "secret"
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var session = (await registration.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.DoesNotContain("refreshToken", await registration.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store", registration.Headers.CacheControl?.ToString());
        Assert.Contains(registration.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("__Host-Buscarro.Refresh=") && cookie.Contains("httponly"));
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BuscarroDbContext>();
            db.FavoriteVehicles.Add(new FavoriteVehicle { UserId = session.User.Id, Provider = "Test", ExternalId = "123", Title = "Veiculo" });
            await db.SaveChangesAsync();
        }
        using var favorites = await host.GetAsync("/api/favorites", session.Token);
        Assert.Equal(HttpStatusCode.OK, favorites.StatusCode);
        Assert.Contains("123", await favorites.Content.ReadAsStringAsync());
        var expired = host.CreateExpiredToken(session);
        using var rejected = await host.GetAsync("/api/favorites", expired);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        using var refresh = await host.PostAsync("/api/auth/refresh", new
        {
        }, expired);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var renewed = (await refresh.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.Equal(session.User.Id, renewed.User.Id);
        Assert.Equal(session.SessionExpiresAtUtc, renewed.SessionExpiresAtUtc);
        Assert.NotEqual(session.Token, renewed.Token);
        using var afterRefresh = await host.GetAsync("/api/favorites", renewed.Token);
        Assert.Equal(HttpStatusCode.OK, afterRefresh.StatusCode);
        Assert.Contains("123", await afterRefresh.Content.ReadAsStringAsync());
        using var logout = await host.PostAsync("/api/auth/logout", new
        {
        }, expired);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var afterLogout = await host.GetAsync("/api/favorites", renewed.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Refresh_reuse_revokes_the_rotated_session()
    {
        await using var host = await AuthHttpFixture.StartAsync();
        await host.BootstrapCsrfAsync();
        using var registered = await host.PostAsync("/api/auth/register", new
        {
            name = "Alex",
            email = "alex@example.com",
            password = "secret"
        });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var oldCookie = host.Cookies["__Host-Buscarro.Refresh"];
        using var refresh = await host.PostAsync("/api/auth/refresh", new
        {
        });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var renewed = (await refresh.Content.ReadFromJsonAsync<AuthResponse>())!;
        host.Cookies["__Host-Buscarro.Refresh"] = oldCookie;
        using var reuse = await host.PostAsync("/api/auth/refresh", new
        {
        });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        using var protectedResponse = await host.GetAsync("/api/favorites", renewed.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task Auth_posts_require_csrf_and_trusted_origin()
    {
        await using var host = await AuthHttpFixture.StartAsync();
        using var missing = await host.PostAsync("/api/auth/login", new
        {
            email = "alex@example.com",
            password = "secret"
        });
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        await host.BootstrapCsrfAsync();
        using var evil = await host.PostAsync("/api/auth/logout", new
        {
        }, origin: "https://evil.test");
        Assert.Equal(HttpStatusCode.Forbidden, evil.StatusCode);
        using var missingOrigin = await host.PostAsync("/api/auth/logout", new
        {
        }, origin: null);
        Assert.Equal(HttpStatusCode.Forbidden, missingOrigin.StatusCode);
    }

    [Fact]
    public async Task Google_creates_user_and_uses_same_refresh_session()
    {
        await using var host = await AuthHttpFixture.StartAsync();
        await host.BootstrapCsrfAsync();
        using var challenge = await host.PostAsync("/api/auth/google/challenge", new
        {
        });
        Assert.Equal(HttpStatusCode.OK, challenge.StatusCode);
        var data = (await challenge.Content.ReadFromJsonAsync<GoogleChallengeResponse>())!;
        host.GoogleIdentity = new GoogleIdentity("google-sub", "alex@gmail.com", true, "Alex", null, data.Nonce);
        using var login = await host.PostAsync("/api/auth/google", new
        {
            idToken = "validated-by-test-provider",
            challengeId = data.ChallengeId
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        using var refresh = await host.PostAsync("/api/auth/refresh", new
        {
        });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Equal(auth.User.Id, (await refresh.Content.ReadFromJsonAsync<AuthResponse>())!.User.Id);
        using var replay = await host.PostAsync("/api/auth/google", new
        {
            idToken = "validated-by-test-provider",
            challengeId = data.ChallengeId
        });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }
}
