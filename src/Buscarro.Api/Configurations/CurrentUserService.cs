using System.Security.Claims;
using Buscarro.Application.Interfaces;

namespace Buscarro.Api.Configurations;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User.Identity?.IsAuthenticated != true) return null;
            var value = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

}
