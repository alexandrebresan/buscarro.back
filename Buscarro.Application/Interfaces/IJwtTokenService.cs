using Buscarro.Application.DTOs;
using Buscarro.Domain.Entities;

namespace Buscarro.Application.Interfaces;

public interface IJwtTokenService
{
    AccessTokenResponse CreateToken(User user);
}
