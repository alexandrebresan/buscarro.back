using Buscarro.Application.DTOs;
using Buscarro.Domain.Entities;
namespace Buscarro.Application.Interfaces;
public interface IJwtTokenService
{
    JwtTokenResponse CreateToken(User user, UserSession session);
}
