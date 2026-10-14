using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
namespace Buscarro.Infrastructure.Services;
public sealed class GoogleAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
}
public sealed class GoogleTokenValidator(IOptions<GoogleAuthOptions> options) : IGoogleTokenValidator
{
    public async Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ClientId))
            throw new AuthUnavailableException("google_not_configured");
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings { Audience = [options.Value.ClientId], ExpirationTimeClockTolerance = TimeSpan.Zero }).WaitAsync(ct);
            if (payload.Issuer != "accounts.google.com" && payload.Issuer != "https://accounts.google.com")
                throw new AuthFlowException("google_identity_invalid");
            return new(payload.Subject, payload.Email, payload.EmailVerified, payload.Name, payload.HostedDomain, payload.Nonce);
        }
        catch (InvalidJwtException) { throw new AuthFlowException("google_identity_invalid"); }
        catch (HttpRequestException ex) { throw new AuthUnavailableException("google_unavailable", ex); }
    }
}
