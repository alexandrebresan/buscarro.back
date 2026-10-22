using System.Security.Claims;
using Buscarro.Api.Configurations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Buscarro.Tests;

public sealed class AuthTransportTests
{
    [Fact]
    public void Current_user_ignores_unauthenticated_claims()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())]))
        };
        Assert.Null(new CurrentUserService(new HttpContextAccessor { HttpContext = context }).UserId);
    }

    [Fact]
    public async Task Antiforgery_uses_anonymous_identity_and_restores_principal_on_failure()
    {
        var original = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Bearer"));
        var context = new DefaultHttpContext { User = original };
        context.Request.Headers.Origin = "https://buscarro.test";
        context.Request.ContentType = "application/json";
        var antiforgery = new InspectingAntiforgery();
        var service = new AuthAntiforgeryService(antiforgery, Options.Create(new AuthTransportOptions
        {
            AllowedOrigins = ["https://buscarro.test"]
        }));
        Assert.Equal("csrf", service.GetToken(context));
        Assert.Same(original, context.User);
        await Assert.ThrowsAsync<AntiforgeryValidationException>(() => service.ValidateAsync(context));
        Assert.Same(original, context.User);
        Assert.True(antiforgery.SawAnonymous);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("https://attacker.test")]
    public async Task Antiforgery_rejects_missing_or_untrusted_origin(string? origin)
    {
        var context = new DefaultHttpContext();
        if (origin is not null)
            context.Request.Headers.Origin = origin;
        context.Request.ContentType = "application/json";
        var service = new AuthAntiforgeryService(new InspectingAntiforgery(), Options.Create(new AuthTransportOptions
        {
            AllowedOrigins = ["https://buscarro.test"]
        }));
        await Assert.ThrowsAsync<AntiforgeryValidationException>(() => service.ValidateAsync(context));
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/x-www-form-urlencoded")]
    public async Task Antiforgery_rejects_simple_content_types(string contentType)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Origin = "https://buscarro.test";
        context.Request.ContentType = contentType;
        var service = new AuthAntiforgeryService(new InspectingAntiforgery(), Options.Create(new AuthTransportOptions
        {
            AllowedOrigins = ["https://buscarro.test"]
        }));
        await Assert.ThrowsAsync<AntiforgeryValidationException>(() => service.ValidateAsync(context));
    }

    [Fact]
    public void Refresh_cookie_is_host_only_secure_and_httponly()
    {
        var context = new DefaultHttpContext();
        AuthCookies.SetRefresh(context, "secret", DateTime.UtcNow.AddDays(30));
        var header = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("__Host-Buscarro.Refresh=secret", header);
        Assert.Contains("secure", header);
        Assert.Contains("httponly", header);
        Assert.Contains("samesite=none", header);
        Assert.Contains("path=/", header);
        Assert.DoesNotContain("domain=", header);
    }

    [Fact]
    public void Google_browser_cookie_allows_cross_site_requests_without_widening_domain()
    {
        var context = new DefaultHttpContext();
        var binding = AuthCookies.GetOrCreateBrowserBinding(context);
        var cookie = Microsoft.Net.Http.Headers.SetCookieHeaderValue.Parse(context.Response.Headers.SetCookie.ToString());
        Assert.Equal(AuthCookies.BrowserName, cookie.Name.ToString());
        Assert.Equal(binding, cookie.Value.ToString());
        Assert.Equal(64, binding.Length);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.None, cookie.SameSite);
        Assert.True(cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.Equal("/", cookie.Path.ToString());
        Assert.False(cookie.Domain.HasValue);
    }

    [Fact]
    public void Refresh_cookie_removal_preserves_cross_site_security_attributes()
    {
        var context = new DefaultHttpContext();
        AuthCookies.ClearRefresh(context);
        var cookie = Microsoft.Net.Http.Headers.SetCookieHeaderValue.Parse(context.Response.Headers.SetCookie.ToString());
        Assert.Equal(AuthCookies.RefreshName, cookie.Name.ToString());
        Assert.Equal(string.Empty, cookie.Value.ToString());
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.None, cookie.SameSite);
        Assert.True(cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.Equal("/", cookie.Path.ToString());
        Assert.False(cookie.Domain.HasValue);
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);
    }

    private sealed class InspectingAntiforgery : IAntiforgery
    {
        public bool SawAnonymous
        {
            get; private set;
        }
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext context)
        {
            SawAnonymous = context.User.Identity?.IsAuthenticated != true;
            return new("csrf", "cookie", "field", "X-CSRF-Token");
        }
        public AntiforgeryTokenSet GetTokens(HttpContext context) => GetAndStoreTokens(context);
        public Task<bool> IsRequestValidAsync(HttpContext context) => Task.FromResult(false);
        public Task ValidateRequestAsync(HttpContext context)
        {
            SawAnonymous = context.User.Identity?.IsAuthenticated != true;
            throw new AntiforgeryValidationException("invalid");
        }
        public void SetCookieTokenAndHeader(HttpContext context)
        {
        }
    }
}
