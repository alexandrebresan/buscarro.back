using MediatR;
using Buscarro.Application.DTOs;
namespace Buscarro.Application.Commands;
public sealed record ReactivatePasswordCommand(Guid UserId, Guid SessionId, string Password) : IRequest<Unit>;
