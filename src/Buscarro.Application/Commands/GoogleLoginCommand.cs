using MediatR;
using Buscarro.Application.DTOs;
namespace Buscarro.Application.Commands;
public sealed record GoogleLoginCommand(string IdToken, Guid ChallengeId, string BrowserBinding, string? Password = null, string? DisplayName = null) : IRequest<SessionResult>;
