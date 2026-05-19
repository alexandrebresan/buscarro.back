using Buscarro.Api.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Buscarro.Tests;

public sealed class FavoritesControllerAuthorizationTests
{
    [Fact]
    public void FavoritesController_requires_authorized_user()
    {
        var hasAuthorizeAttribute = typeof(FavoritesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any();

        Assert.True(hasAuthorizeAttribute);
    }
}
