namespace Buscarro.Domain.Entities;
public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId
    {
        get; set;
    }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc
    {
        get; set;
    }
    public DateTime ExpiresAtUtc
    {
        get; set;
    }
    public DateTime? UsedAtUtc
    {
        get; set;
    }
    public DateTime? RevokedAtUtc
    {
        get; set;
    }
    public Guid? SuccessorId
    {
        get; set;
    }
}
