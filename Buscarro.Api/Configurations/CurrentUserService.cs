using System.Security.Claims;
using Buscarro.Application.Interfaces;

namespace Buscarro.Api.Configurations;

public sealed class CurrentUserService : ICurrentUserService
{
    private const string UserIdSessionKey = "UserId";

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
            var value = httpContext?.Session.GetString(UserIdSessionKey)
                ?? httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }
}
