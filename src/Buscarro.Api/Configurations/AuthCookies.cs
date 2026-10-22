using System.Security.Cryptography;

namespace Buscarro.Api.Configurations;

public static class AuthCookies
{
    public const string RefreshName = "__Host-Buscarro.Refresh";
    public const string BrowserName = "__Host-Buscarro.GoogleBrowser";

    public static void SetRefresh(HttpContext context, string token, DateTime expiresAtUtc)
        => context.Response.Cookies.Append(RefreshName, token, Options(expiresAtUtc));

    public static void ClearRefresh(HttpContext context)
        => context.Response.Cookies.Delete(RefreshName, Options());

    public static string GetOrCreateBrowserBinding(HttpContext context)
    {
        var current = context.Request.Cookies[BrowserName];
        if (current is { Length: 64 } && current.All(Uri.IsHexDigit))
            return current;
        var binding = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        context.Response.Cookies.Append(BrowserName, binding, Options());
        return binding;
    }

    private static CookieOptions Options(DateTime? expiresAtUtc = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.None,
        Path = "/",
        IsEssential = true,
        Expires = expiresAtUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc.Value, DateTimeKind.Utc)) : null
    };
}
