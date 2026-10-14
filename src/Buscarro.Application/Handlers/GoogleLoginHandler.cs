using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using MediatR;
namespace Buscarro.Application.Handlers;
public sealed class GoogleLoginHandler(IUserRepository users, IAuthSessionRepository sessions, IUnitOfWork uow, ISessionIssuer issuer, IGoogleTokenValidator validator, IPasswordHasherService hasher, IRefreshTokenGenerator generator, TimeProvider clock) : IRequestHandler<GoogleLoginCommand, SessionResult>
{
    public async Task<SessionResult> Handle(GoogleLoginCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken) || request.IdToken.Length > 16384 || string.IsNullOrWhiteSpace(request.BrowserBinding) || request.BrowserBinding.Length > 256)
            throw new AuthFlowException("google_identity_invalid");
        var identity = await validator.ValidateAsync(request.IdToken, ct);
        if (string.IsNullOrWhiteSpace(identity.Subject) || identity.Subject.Length > 255 || string.IsNullOrWhiteSpace(identity.Nonce))
            throw new AuthFlowException("google_identity_invalid");
        SessionResult? result = null;
        async Task ExecuteAsync() => await uow.ExecuteInTransactionAsync(async token =>
        {
            result = null;
            await sessions.LockIdentityAsync("Google:" + identity.Subject, token);
            var link = await sessions.FindExternalLoginAsync("Google", identity.Subject, token);
            User? user = null;
            bool automatic = false;
            bool newUser = false;
            if (link is not null)
                user = await sessions.LockUserAsync(link.UserId, token);
            else
            {
                if (!identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email))
                    throw new AuthFlowException("google_email_unverified");
                var email = AuthInput.Email(identity.Email);
                await sessions.LockIdentityAsync("email:" + email, token);
                var candidate = await users.GetByEmailAsync(email, token);
                user = candidate is null ? null : await sessions.LockUserAsync(candidate.Id, token);
                if (user is null)
                {
                    var name = string.IsNullOrWhiteSpace(identity.Name) ? request.DisplayName : identity.Name;
                    if (string.IsNullOrWhiteSpace(name))
                        throw new AuthFlowException("profile_name_required", 409);
                    user = new User { Name = AuthInput.Required(name, "Nome", 120), Email = email, PasswordHash = null, PasswordLoginEnabled = false };
                    newUser = true;
                }
                else
                {
                    var existing = await sessions.FindUserExternalLoginAsync(user.Id, "Google", token);
                    if (existing is not null)
                        throw new AuthFlowException("account_link_conflict", 409);
                    automatic = IsAuthoritative(identity, email);
                    if (!automatic && (string.IsNullOrWhiteSpace(request.Password) || !user.PasswordLoginEnabled || user.PasswordHash is null || !hasher.VerifyPassword(user, request.Password.Trim())))
                        throw new AuthFlowException("account_link_required", 409);
                }
            }
            if (user is null)
                throw new AuthFlowException("session_invalid");
            // User is locked first; challenge lock comes after identity/user locks everywhere.
            var challenge = await sessions.LockChallengeAsync(request.ChallengeId, token);
            var now = clock.GetUtcNow().UtcDateTime;
            if (challenge is null || challenge.UsedAtUtc is not null || challenge.ExpiresAtUtc <= now || challenge.NonceHash != generator.Hash(identity.Nonce!) || challenge.BrowserBindingHash != generator.Hash(request.BrowserBinding))
                throw new AuthFlowException("google_challenge_invalid");
            challenge.UsedAtUtc = now;
            if (newUser)
                await users.AddAsync(user, token);
            if (link is null)
            {
                if (automatic)
                {
                    await sessions.RevokeSessionsAsync(user.Id, null, now, "google_auto_link", token);
                    user.PasswordLoginEnabled = false;
                }
                await sessions.AddExternalLoginAsync(new UserExternalLogin { UserId = user.Id, Subject = identity.Subject, CreatedAtUtc = now }, token);
            }
            result = await issuer.CreateAsync(user, "google", token);
        }, ct);
        try
        {
            await ExecuteAsync();
        }
        catch (UniqueConstraintException)
        {
            // The failed UoW has rolled back and cleared tracking before this canonical read.
            var canonical = await sessions.FindExternalLoginAsync("Google", identity.Subject, ct);
            if (canonical is null)
                throw new AuthFlowException("account_link_conflict", 409);
            try
            {
                await ExecuteAsync();
            }
            catch (UniqueConstraintException) { throw new AuthFlowException("account_link_conflict", 409); }
        }
        return result!;
    }
    private static bool IsAuthoritative(GoogleIdentity identity, string email)
    {
        var domain = email[(email.LastIndexOf('@') + 1)..];
        if (domain.Equals("gmail.com", StringComparison.OrdinalIgnoreCase))
            return true;
        var hd = identity.HostedDomain;
        return !string.IsNullOrWhiteSpace(hd) && hd.Length <= 253 && Uri.CheckHostName(hd) == UriHostNameType.Dns;
    }
}
