using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure;
using Buscarro.Infrastructure.Configurations;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Repositories;
using Buscarro.Infrastructure.Services;
using Buscarro.Infrastructure.UnitOfWork;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

namespace Buscarro.Api.Configurations;

public static class ApiHostComposition
{
    public const string CorsPolicyName = "BuscarroCors";

    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.Configure<ForwardedHeadersOptions>(options => AuthProxyConfiguration.Configure(options, builder.Configuration));
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddCors(options =>
        {
            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];
            options.AddPolicy(CorsPolicyName, policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod());
        });
        builder.Services.AddMemoryCache();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "Buscarro API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Informe o token JWT no formato Bearer."
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    []
                }
            });
        });

        builder.Services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(FindAllVehiclesQuery).Assembly));
        builder.Services.RemoveAll<IRequestHandler<SynchronizeProviderCatalogCommand, CatalogSynchronizationResult>>();
        builder.Services.RemoveAll<IRequestHandler<GetProviderSynchronizationDueQuery, bool>>();
        builder.Services.RemoveAll<IRequestHandler<CleanupCatalogCommand, CatalogCleanupResult>>();
        builder.Services.RemoveAll<IRequestHandler<RollbackCatalogCommand, CatalogRollbackResult>>();

        var connectionString = PostgresConnectionString.Normalize(
            builder.Configuration["DATABASE_URL"]
                ?? builder.Configuration.GetConnectionString("BuscarroDb")
                ?? string.Empty);
        builder.Services.AddDbContext<BuscarroDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                postgres => postgres.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null)),
            optionsLifetime: ServiceLifetime.Singleton);
        builder.Services.AddDbContextFactory<BuscarroDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                postgres => postgres.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null)));
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IFavoriteVehicleRepository, FavoriteVehicleRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        builder.Services.AddOptions<CatalogQueryOptions>()
            .Bind(builder.Configuration.GetSection(CatalogQueryOptions.SectionName))
            .Validate(options => options.CommandTimeoutSeconds > 0 &&
                options.PageCacheTtlSeconds > 0 && options.CountCacheTtlSeconds > 0 &&
                options.CacheSizeLimit > 0, "CatalogQuery options are invalid.")
            .ValidateOnStart();
        builder.Services.AddVersionedCatalogReads();
        builder.Services.AddSingleton<ICatalogReadinessProbe, PostgresCatalogReadinessProbe>();
        builder.Services.AddOptions<CatalogReadinessOptions>()
            .Bind(builder.Configuration.GetSection(CatalogReadinessOptions.SectionName))
            .Validate(options => options.IsValid(), "CatalogReadiness options are invalid.")
            .ValidateOnStart();
        builder.Services.AddHealthChecks()
            .AddCheck<CatalogReadinessHealthCheck>("catalog_readiness", tags: ["ready"]);
        builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddSingleton<IVehicleProviderCatalogReadService, VehicleProviderCatalogReadService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection("Jwt"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer) && !string.IsNullOrWhiteSpace(options.Audience) &&
                options.ExpirationMinutes is >= 1 and <= 60, "Jwt:Issuer, Audience e ExpirationMinutes (1 a 60) devem ser validos.")
            .ValidateOnStart();
        builder.Services.AddAuthenticationCore(builder.Configuration);
        AddAuthTransport(builder);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.CreateSecurityKey(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userIdValue = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        var sessionIdValue = context.Principal?.FindFirst("sid")?.Value;
                        if (!Guid.TryParse(userIdValue, out var userId) || !Guid.TryParse(sessionIdValue, out var sessionId))
                        {
                            context.Fail("Sessao invalida.");
                            return;
                        }
                        var mediator = context.HttpContext.RequestServices.GetRequiredService<IMediator>();
                        if (!await mediator.Send(new ValidateUserSessionQuery(userId, sessionId), context.HttpContext.RequestAborted))
                            context.Fail("Sessao invalida.");
                    },
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception is Buscarro.Application.Exceptions.AuthUnavailableException)
                            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(context.Exception).Throw();
                        return Task.CompletedTask;
                    }
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddAiSearch(builder.Configuration);
    }

    private static void AddAuthTransport(WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<AuthTransportOptions>()
            .Configure(options =>
            {
                builder.Configuration.GetSection("Auth").Bind(options);
                if (options.AllowedOrigins.Length == 0)
                    options.AllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            })
            .Validate(options => options.AllowedOrigins.Length > 0 && options.AllowedOrigins.All(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
                uri.GetLeftPart(UriPartial.Authority) == origin), "Auth:AllowedOrigins deve conter origens HTTPS exatas.")
            .ValidateOnStart();
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-Token";
            options.Cookie.Name = "__Host-Buscarro.Csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
        });
        builder.Services.AddScoped<AuthAntiforgeryService>();
        builder.Services.AddScoped<AuthTransportFilter>();
        var protection = builder.Services.AddDataProtection()
            .SetApplicationName("Buscarro.Authentication")
            .PersistKeysToDbContext<BuscarroDbContext>();
        var certificatePath = builder.Configuration["Auth:DataProtectionCertificatePath"];
        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath, builder.Configuration["Auth:DataProtectionCertificatePassword"],
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.EphemeralKeySet);
            if (!certificate.HasPrivateKey) throw new InvalidOperationException("Certificado Data Protection requer chave privada.");
            protection.ProtectKeysWithCertificate(certificate);
            var previousPaths = builder.Configuration.GetSection("Auth:PreviousDataProtectionCertificatePaths").Get<string[]>() ?? [];
            var certificates = previousPaths.Select(path =>
                System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(path,
                    builder.Configuration["Auth:DataProtectionCertificatePassword"],
                    System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.EphemeralKeySet)).Prepend(certificate).ToArray();
            if (certificates.Any(previous => !previous.HasPrivateKey))
                throw new InvalidOperationException("Certificados Data Protection requerem chave privada.");
            protection.UnprotectKeysWithAnyCertificate(certificates);
        }
        else if (!builder.Environment.IsDevelopment())
            throw new InvalidOperationException("Auth:DataProtectionCertificatePath obrigatorio fora de Development.");
        builder.Services.AddHostedService<AuthCleanupWorker>();
    }
}
