using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Buscarro.Api.Configurations;

public sealed class AuthAntiforgeryService(IAntiforgery antiforgery, IOptions<AuthTransportOptions> options)
{
    public string GetToken(HttpContext context)
    {
        var original = context.User;
        try
        {
            // Auth cookies must work both before login and with an expired Bearer token.
            // Keep the antiforgery identity stable; never execute application code here.
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            return antiforgery.GetAndStoreTokens(context).RequestToken
                ?? throw new InvalidOperationException("Nao foi possivel emitir o token CSRF.");
        }
        finally { context.User = original; }
    }

    public async Task ValidateAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin) || origin == "null" ||
            !options.Value.AllowedOrigins.Contains(origin, StringComparer.Ordinal))
            throw new AntiforgeryValidationException("Origem nao autorizada.");
        if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var contentType) ||
            !string.Equals(contentType.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase))
            throw new AntiforgeryValidationException("Content-Type deve ser application/json.");
        var original = context.User;
        try
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            await antiforgery.ValidateRequestAsync(context);
        }
        finally { context.User = original; }
    }
}
