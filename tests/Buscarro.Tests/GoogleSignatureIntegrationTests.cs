using Buscarro.Application.Exceptions;
using Buscarro.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Buscarro.Tests;

public sealed class GoogleSignatureIntegrationTests
{
    [GooglePublicKeysFact]
    [Trait("Category", "GooglePublicKeysIntegration")]
    public async Task Untrusted_rsa_signature_is_rejected_using_google_public_keys()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var token = GoogleTokenValidatorTests.Token(new
        {
            alg = "RS256",
            kid = "untrusted-key",
            typ = "JWT"
        }, new
        {
            aud = "our-client",
            iss = "https://accounts.google.com",
            sub = "subject",
            iat = now - 60,
            exp = now + 600,
            nonce = "nonce"
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var validator = new GoogleTokenValidator(Options.Create(new GoogleAuthOptions { ClientId = "our-client" }));
        var error = await Assert.ThrowsAsync<AuthFlowException>(() => validator.ValidateAsync(token, deadline.Token));
        Assert.Equal("google_identity_invalid", error.Code);
    }
}

public sealed class GooglePublicKeysFactAttribute : FactAttribute
{
    public GooglePublicKeysFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_GOOGLE_SIGNATURE_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
            Skip = "Validacao RSA com chaves publicas Google NAO EXECUTADA: RUN_GOOGLE_SIGNATURE_TESTS=true ausente.";
    }
}
