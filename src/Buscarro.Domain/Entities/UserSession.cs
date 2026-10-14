namespace Buscarro.Domain.Entities;
public sealed class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId
    {
        get; set;
    }
    public DateTime CreatedAtUtc
    {
        get; set;
    }
    public DateTime ExpiresAtUtc
    {
        get; set;
    }
    public DateTime? RevokedAtUtc
    {
        get; set;
    }
    public string? RevocationReason
    {
        get; set;
    }
    public string AuthenticationMethod { get; set; } = "password";
    public DateTime AuthenticatedAtUtc
    {
        get; set;
    }
    public bool IsActive(DateTime now) => RevokedAtUtc is null && ExpiresAtUtc > now;
    public void Revoke(DateTime now, string reason)
    {
        RevokedAtUtc ??= now;
        RevocationReason ??= reason;
    }
}
