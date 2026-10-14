namespace Buscarro.Api.Configurations;

using Microsoft.IdentityModel.Tokens;
using System.Text;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 15;

    public SymmetricSecurityKey CreateSecurityKey()
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(RequireSecret()));
    }

    private string RequireSecret()
    {
        var secret = Require(Secret, "Jwt:Secret");

        if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("Jwt:Secret deve ter pelo menos 32 bytes.");
        }

        return secret;
    }

    public static string Require(string value, string configurationKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{configurationKey} nao configurado.");
        }

        return value.Trim();
    }
}
