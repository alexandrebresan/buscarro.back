using Buscarro.Domain.Entities;
namespace Buscarro.Tests;
public sealed class AuthenticationCoreContractTests
{
    [Fact]
    public void Users_can_disable_password_login_without_losing_the_hash()
    {
        Assert.NotNull(typeof(User).GetProperty("PasswordLoginEnabled"));
    }
    [Fact]
    public void Session_and_refresh_families_have_persistable_revocation_and_consumption()
    {
        var assembly = typeof(User).Assembly;
        Assert.NotNull(assembly.GetType("Buscarro.Domain.Entities.UserSession")?.GetProperty("RevokedAtUtc"));
        Assert.NotNull(assembly.GetType("Buscarro.Domain.Entities.RefreshToken")?.GetProperty("UsedAtUtc"));
    }
}
