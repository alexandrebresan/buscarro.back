using Buscarro.Application.DTOs;
namespace Buscarro.Application.Interfaces;
public interface IGoogleTokenValidator
{
    Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken);
}
