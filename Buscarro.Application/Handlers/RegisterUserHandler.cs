using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class RegisterUserHandler : IRequestHandler<RegisterUserCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterUserHandler(
        IUserRepository users,
        IPasswordHasherService passwordHasher,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var name = NormalizeRequired(request.Name, "Nome");
        var email = NormalizeEmail(request.Email);
        var password = NormalizeRequired(request.Password, "Senha");

        if (await _users.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new ConflictException("Ja existe um usuario cadastrado com este e-mail.");
        }

        var user = new User
        {
            Name = name,
            Email = email
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        await _users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
            throw new ValidationException("E-mail invalido.");
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
