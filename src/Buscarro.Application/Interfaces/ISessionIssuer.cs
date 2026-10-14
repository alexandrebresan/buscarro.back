using Buscarro.Application.DTOs;
using Buscarro.Domain.Entities;
namespace Buscarro.Application.Interfaces;
public interface ISessionIssuer
{
    Task<SessionResult> CreateAsync(User user, string authenticationMethod, CancellationToken cancellationToken);
    Task<SessionResult> RotateAsync(User user, UserSession session, RefreshToken previous, CancellationToken cancellationToken);
}
