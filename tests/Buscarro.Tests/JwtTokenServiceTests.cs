using Buscarro.Api.Configurations;
using Buscarro.Domain.Entities;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Buscarro.Tests;

public sealed class JwtTokenServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateSecurityKey_throws_clear_error_when_secret_is_missing(string secret)
    {
        var options = new JwtOptions { Secret = secret };

        var exception = Assert.Throws<InvalidOperationException>(() => options.CreateSecurityKey());

        Assert.Equal("Jwt:Secret nao configurado.", exception.Message);
    }

    [Fact]
    public void CreateSecurityKey_throws_clear_error_when_secret_is_too_short()
    {
        var options = new JwtOptions { Secret = "short-secret" };

        var exception = Assert.Throws<InvalidOperationException>(() => options.CreateSecurityKey());

        Assert.Equal("Jwt:Secret deve ter pelo menos 32 bytes.", exception.Message);
    }

    [Fact]
    public void CreateSecurityKey_returns_key_when_secret_is_valid()
    {
        var options = new JwtOptions
        {
            Secret = "buscarro-test-secret-with-at-least-32-bytes"
        };

        var key = options.CreateSecurityKey();

        Assert.NotEmpty(key.Key);
    }

    [Fact]
    public void CreateToken_includes_user_id_name_identifier_claim()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Alex",
            Email = "alex@example.com"
        };
        var service = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                Issuer = "Buscarro",
                Audience = "Buscarro.Api",
                Secret = "buscarro-test-secret-with-at-least-32-bytes",
                ExpirationMinutes = 60
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero)));

        var result = service.CreateToken(user);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(new DateTime(2026, 5, 18, 13, 0, 0, DateTimeKind.Utc), result.ExpiresAtUtc);
        Assert.Contains(token.Claims, claim =>
            claim.Type == ClaimTypes.NameIdentifier &&
            claim.Value == user.Id.ToString());
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }
}
