using MediatR;
using Buscarro.Application.DTOs;
namespace Buscarro.Application.Commands;
public sealed record LogoutSessionCommand(string? RefreshToken) : IRequest<Unit>;
