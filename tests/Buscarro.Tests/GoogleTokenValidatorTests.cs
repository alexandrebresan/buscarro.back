using System.Text;
using System.Text.Json;
using Buscarro.Application.Exceptions;
using Buscarro.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Buscarro.Tests;

public sealed class GoogleTokenValidatorTests
{
    [Fact]
    public async Task Malformed_token_is_rejected_by_real_google_validator()
    {
        var validator = new GoogleTokenValidator(Options.Create(new GoogleAuthOptions { ClientId = "our-client" }));
        var error = await Assert.ThrowsAsync<AuthFlowException>(() => validator.ValidateAsync("not-a-token", default));
        Assert.Equal("google_identity_invalid", error.Code);
    }

    [Theory]
    [InlineData("other-client", "https://accounts.google.com", false)]
    [InlineData("our-client", "https://evil.test", false)]
    [InlineData("our-client", "https://accounts.google.com", true)]
    public async Task Wrong_audience_issuer_and_expiration_are_rejected_by_real_google_validator(string audience, string issuer, bool expired)
    {
        var validator = new GoogleTokenValidator(Options.Create(new GoogleAuthOptions { ClientId = "our-client" }));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var jwt = Token(new
        {
            alg = "RS256",
            kid = "untrusted-key",
            typ = "JWT"
        }, new
        {
            aud = audience,
            iss = issuer,
            sub = "subject",
            iat = now - 120,
            exp = expired ? now - 60 : now + 600,
            email = "alex@gmail.com",
            email_verified = true,
            nonce = "nonce"
        });
        var error = await Assert.ThrowsAsync<AuthFlowException>(() => validator.ValidateAsync(jwt, default));
        Assert.Equal("google_identity_invalid", error.Code);
    }

    [Fact]
    public async Task Unsupported_signature_algorithm_is_rejected()
    {
        var validator = new GoogleTokenValidator(Options.Create(new GoogleAuthOptions { ClientId = "our-client" }));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var jwt = Token(new
        {
            alg = "HS256",
            typ = "JWT"
        }, new
        {
            aud = "our-client",
            iss = "https://accounts.google.com",
            sub = "subject",
            iat = now,
            exp = now + 600
        });
        await Assert.ThrowsAsync<AuthFlowException>(() => validator.ValidateAsync(jwt, default));
    }

    [Fact]
    public async Task Missing_client_configuration_is_operational_error()
    {
        var validator = new GoogleTokenValidator(Options.Create(new GoogleAuthOptions()));
        var error = await Assert.ThrowsAsync<AuthUnavailableException>(() => validator.ValidateAsync("token", default));
        Assert.Equal("google_not_configured", error.Code);
    }

    internal static string Token(object header, object payload)
    {
        static string Encode(object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return Encode(header) + "." + Encode(payload) + "." + Convert.ToBase64String(new byte[256]).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
