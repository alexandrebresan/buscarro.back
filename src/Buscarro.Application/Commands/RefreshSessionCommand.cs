using MediatR;
using Buscarro.Application.DTOs;
namespace Buscarro.Application.Commands;
public sealed record RefreshSessionCommand(string? RefreshToken) : IRequest<SessionResult>;
