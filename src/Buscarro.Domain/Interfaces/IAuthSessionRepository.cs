using Buscarro.Domain.Entities;
namespace Buscarro.Domain.Interfaces;
public interface IAuthSessionRepository
{
    Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserSession?> LockSessionAsync(Guid sessionId, CancellationToken cancellationToken);
    Task LockIdentityAsync(string identity, CancellationToken cancellationToken);
    Task<UserSession?> GetSessionAsync(Guid id, CancellationToken cancellationToken);
    Task<RefreshToken?> FindRefreshAsync(string hash, CancellationToken cancellationToken);
    Task AddSessionAsync(UserSession session, RefreshToken token, CancellationToken cancellationToken);
    Task AddRefreshAsync(RefreshToken token, CancellationToken cancellationToken);
    Task RevokeSessionsAsync(Guid userId, Guid? exceptSessionId, DateTime now, string reason, CancellationToken cancellationToken);
    Task RevokeRefreshTokensAsync(Guid sessionId, DateTime now, CancellationToken cancellationToken);
    Task<UserExternalLogin?> FindExternalLoginAsync(string provider, string subject, CancellationToken cancellationToken);
    Task<UserExternalLogin?> FindUserExternalLoginAsync(Guid userId, string provider, CancellationToken cancellationToken);
    Task AddExternalLoginAsync(UserExternalLogin login, CancellationToken cancellationToken);
    Task AddChallengeAsync(GoogleAuthChallenge challenge, CancellationToken cancellationToken);
    Task<GoogleAuthChallenge?> LockChallengeAsync(Guid id, CancellationToken cancellationToken);
    Task CleanupAsync(DateTime now, CancellationToken cancellationToken);
}
