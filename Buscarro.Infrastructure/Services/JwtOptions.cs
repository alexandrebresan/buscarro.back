namespace Buscarro.Infrastructure.Services;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "Buscarro";
    public string Audience { get; set; } = "Buscarro";
    public string SecretKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}
