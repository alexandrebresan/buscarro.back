using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using Buscarro.Application.Exceptions;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Buscarro.Infrastructure.Repositories;
public sealed class AuthSessionRepository(BuscarroDbContext db) : IAuthSessionRepository
{
    private static async Task<T> Guard<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (NpgsqlException ex) { throw new AuthUnavailableException(inner: ex); }
    }
    private static async Task Guard(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (NpgsqlException ex) { throw new AuthUnavailableException(inner: ex); }
    }
    private T? TrackFresh<T>(T? entity) where T : class
    {
        if (entity is null)
            return null;
        var entry = db.Entry(entity);
        var key = entry.Metadata.FindPrimaryKey()!;
        var existing = db.ChangeTracker.Entries<T>().FirstOrDefault(e => key.Properties.All(p => Equals(e.Property(p.Name).CurrentValue, entry.Property(p.Name).CurrentValue)));
        if (existing is not null)
        {
            existing.CurrentValues.SetValues(entry.CurrentValues);
            existing.OriginalValues.SetValues(entry.CurrentValues);
            existing.State = EntityState.Unchanged;
            return existing.Entity;
        }
        db.Attach(entity);
        return entity;
    }
    public Task<User?> LockUserAsync(Guid id, CancellationToken ct) => Guard(async () => TrackFresh(await (db.Database.IsNpgsql() ? db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\"={id} FOR UPDATE") : db.Users.Where(x => x.Id == id)).AsNoTracking().SingleOrDefaultAsync(ct)));
    public Task<UserSession?> LockSessionAsync(Guid id, CancellationToken ct) => Guard(async () => TrackFresh(await (db.Database.IsNpgsql() ? db.UserSessions.FromSqlInterpolated($"SELECT * FROM \"UserSessions\" WHERE \"Id\"={id} FOR UPDATE") : db.UserSessions.Where(x => x.Id == id)).AsNoTracking().SingleOrDefaultAsync(ct)));
    public Task<GoogleAuthChallenge?> LockChallengeAsync(Guid id, CancellationToken ct) => Guard(async () => TrackFresh(await (db.Database.IsNpgsql() ? db.GoogleAuthChallenges.FromSqlInterpolated($"SELECT * FROM \"GoogleAuthChallenges\" WHERE \"Id\"={id} FOR UPDATE") : db.GoogleAuthChallenges.Where(x => x.Id == id)).AsNoTracking().SingleOrDefaultAsync(ct)));
    public Task LockIdentityAsync(string identity, CancellationToken ct) => Guard(async () => { if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({identity},0))", ct); });
    public Task<UserSession?> GetSessionAsync(Guid id, CancellationToken ct) => Guard(() => db.UserSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct));
    public Task<RefreshToken?> FindRefreshAsync(string hash, CancellationToken ct) => Guard(async () => TrackFresh(await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct)));
    public Task AddSessionAsync(UserSession session, RefreshToken token, CancellationToken ct) => Guard(async () => { await db.UserSessions.AddAsync(session, ct); await db.RefreshTokens.AddAsync(token, ct); });
    public Task AddRefreshAsync(RefreshToken token, CancellationToken ct) => Guard(async () => { await db.RefreshTokens.AddAsync(token, ct); });
    public Task RevokeSessionsAsync(Guid userId, Guid? except, DateTime now, string reason, CancellationToken ct) => Guard(async () =>
    {
        // Caller holds the user lock. Lock existing sessions in deterministic order.
        var ids = await db.UserSessions.AsNoTracking().Where(x => x.UserId == userId && x.Id != except && x.RevokedAtUtc == null).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            var session = await LockSessionAsync(id, ct);
            session?.Revoke(now, reason);
            await RevokeRefreshTokensAsync(id, now, ct);
        }
    });
    public Task RevokeRefreshTokensAsync(Guid sessionId, DateTime now, CancellationToken ct) => Guard(async () => { var tokens = await db.RefreshTokens.Where(x => x.SessionId == sessionId).ToListAsync(ct); foreach (var token in tokens) token.RevokedAtUtc ??= now; });
    public Task<UserExternalLogin?> FindExternalLoginAsync(string provider, string subject, CancellationToken ct) => Guard(() => db.UserExternalLogins.AsNoTracking().SingleOrDefaultAsync(x => x.Provider == provider && x.Subject == subject, ct));
    public Task<UserExternalLogin?> FindUserExternalLoginAsync(Guid id, string provider, CancellationToken ct) => Guard(() => db.UserExternalLogins.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == id && x.Provider == provider, ct));
    public Task AddExternalLoginAsync(UserExternalLogin login, CancellationToken ct) => Guard(async () => { await db.UserExternalLogins.AddAsync(login, ct); });
    public Task AddChallengeAsync(GoogleAuthChallenge challenge, CancellationToken ct) => Guard(async () => { await db.GoogleAuthChallenges.AddAsync(challenge, ct); });
    public Task CleanupAsync(DateTime now, CancellationToken ct) => Guard(async () =>
    {
        var challenges = await db.GoogleAuthChallenges.Where(x => x.ExpiresAtUtc <= now).ToListAsync(ct);
        db.GoogleAuthChallenges.RemoveRange(challenges);
        var expired = await db.UserSessions.AsNoTracking().Where(x => x.ExpiresAtUtc <= now).Select(x => new { x.Id, x.UserId }).OrderBy(x => x.UserId).ThenBy(x => x.Id).ToListAsync(ct);
        foreach (var family in expired)
        {
            await LockUserAsync(family.UserId, ct);
            var session = await LockSessionAsync(family.Id, ct);
            if (session is null || session.ExpiresAtUtc > now)
                continue;
            var tokens = await db.RefreshTokens.Where(x => x.SessionId == family.Id).ToListAsync(ct);
            db.RefreshTokens.RemoveRange(tokens);
            db.UserSessions.Remove(session);
        }
    });
}
