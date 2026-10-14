namespace Buscarro.Domain.Entities;
public sealed class UserExternalLogin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId
    {
        get; set;
    }
    public string Provider { get; set; } = "Google";
    public string Subject { get; set; } = string.Empty;
    public DateTime CreatedAtUtc
    {
        get; set;
    }
}
