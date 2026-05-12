using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class LoginUserHandler : IRequestHandler<LoginUserCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginUserHandler(
        IUserRepository users,
        IPasswordHasherService passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResponse> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var password = NormalizeRequired(request.Password, "Senha");
        var user = await _users.GetByEmailAsync(email, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(user, password))
        {
            throw new AuthenticationFailedException();
        }

        return ToAuthResponse(user, _jwtTokenService.CreateToken(user));
    }

    private static AuthResponse ToAuthResponse(User user, AccessTokenResponse accessToken)
    {
        return new AuthResponse
        {
            User = new UserResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email
            },
            AccessToken = accessToken
        };
    }

    private static string NormalizeEmail(string? value)
    {
        var email = NormalizeRequired(value, "E-mail").ToLowerInvariant();

        if (!email.Contains('@', StringComparison.Ordinal))
        {
            throw new AuthenticationFailedException();
        }

        return email;
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{fieldName} e obrigatorio.");
        }

        return value.Trim();
    }
}
