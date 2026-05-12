using System.Diagnostics;
using System.Text;
using Buscarro.Api.Configurations;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Buscarro.Infrastructure.Providers;
using Buscarro.Infrastructure.Repositories;
using Buscarro.Infrastructure.Services;
using Buscarro.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var jwtOptions = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Buscarro API",
        Version = "v1"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Informe o token JWT usando o formato: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(FindAllVehiclesQuery).Assembly));

builder.Services.AddDbContext<BuscarroDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BuscarroDb")));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IFavoriteVehicleRepository, FavoriteVehicleRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpClient<CarzinVehicleProvider>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<CarzinOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<VehicleCacheOptions>(
    builder.Configuration.GetSection("VehicleCache"));
builder.Services.Configure<CarzinOptions>(
    builder.Configuration.GetSection("Carzin"));

builder.Services.AddTransient<IVehicleProvider>(serviceProvider =>
    serviceProvider.GetRequiredService<CarzinVehicleProvider>());
builder.Services.AddSingleton<IVehicleCatalogReadService, VehicleCatalogReadService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var swaggerUrl = app.Urls
            .OrderByDescending(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .Select(url => $"{url.TrimEnd('/')}/swagger")
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(swaggerUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = swaggerUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignora ambientes sem suporte para abrir o navegador automaticamente.
        }
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
