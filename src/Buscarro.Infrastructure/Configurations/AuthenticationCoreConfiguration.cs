using Buscarro.Application.Interfaces;
using Buscarro.Application.DTOs;
using Buscarro.Application.Services;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Repositories;
using Buscarro.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Buscarro.Infrastructure.Configurations;
public static class AuthenticationCoreConfiguration
{
    public static IServiceCollection AddAuthenticationCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<AuthSessionOptions>().Bind(configuration.GetSection("Auth"))
         .Validate(options => options.SessionDays >= 1 && options.SessionDays <= 90, "Auth:SessionDays must be between 1 and 90.").ValidateOnStart();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
        services.AddScoped<ISessionIssuer, SessionIssuer>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.Configure<GoogleAuthOptions>(configuration.GetSection("Google"));
        return services;
    }
}
