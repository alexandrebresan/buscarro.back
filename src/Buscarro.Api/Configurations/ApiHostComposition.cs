using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Repositories;
using Buscarro.Infrastructure.Services;
using Buscarro.Infrastructure.UnitOfWork;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.Cookie.Name = ".Buscarro.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.IdleTimeout = TimeSpan.FromHours(8);
        });
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
        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
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
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddAiSearch(builder.Configuration);
    }
}
