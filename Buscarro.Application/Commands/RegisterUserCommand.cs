using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Commands;

public sealed record RegisterUserCommand(
    string Name,
    string Email,
    string Password) : IRequest<AuthResponse>;
