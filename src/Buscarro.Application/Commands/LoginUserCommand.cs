using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Commands;

public sealed record LoginUserCommand(
    string Email,
    string Password) : IRequest<AuthResponse>;
