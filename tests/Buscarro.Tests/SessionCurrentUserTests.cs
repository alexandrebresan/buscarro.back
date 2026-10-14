using Buscarro.Api.Configurations;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Buscarro.Tests;

public sealed class SessionCurrentUserTests
{
    [Fact]
    public void CurrentUserService_requires_authenticated_principal()
    {
        var context = new DefaultHttpContext();
        Assert.Null(new CurrentUserService(new HttpContextAccessor { HttpContext = context }).UserId);
    }

    [Fact]
    public void CurrentUserService_reads_user_id_from_authenticated_claim()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Bearer"))
        };
        Assert.Equal(userId, new CurrentUserService(new HttpContextAccessor { HttpContext = context }).UserId);
    }
}
