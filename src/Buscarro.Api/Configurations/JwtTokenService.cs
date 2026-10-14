using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Buscarro.Api.Configurations;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(
        IOptions<JwtOptions> options,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public JwtTokenResponse CreateToken(User user, UserSession session)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAtUtc = now.AddMinutes(Math.Max(1, _options.ExpirationMinutes));
        if (expiresAtUtc > session.ExpiresAtUtc)
            expiresAtUtc = session.ExpiresAtUtc;
        if (session.UserId != user.Id || !session.IsActive(now))
            throw new InvalidOperationException("Sessao invalida para emissao do token.");
        var key = _options.CreateSecurityKey();
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.Name),
            new Claim("sid", session.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };
        var token = new JwtSecurityToken(
            issuer: Require(_options.Issuer, "Jwt:Issuer"),
            audience: Require(_options.Audience, "Jwt:Audience"),
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtTokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            expiresAtUtc);
    }

    private static string Require(string value, string configurationKey)
    {
        return JwtOptions.Require(value, configurationKey);
    }
}
