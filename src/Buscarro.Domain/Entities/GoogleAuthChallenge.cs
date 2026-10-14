namespace Buscarro.Domain.Entities;
public sealed class GoogleAuthChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NonceHash { get; set; } = string.Empty;
    public string BrowserBindingHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc
    {
        get; set;
    }
    public DateTime? UsedAtUtc
    {
        get; set;
    }
}
