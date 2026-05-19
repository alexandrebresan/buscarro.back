using System.Diagnostics;
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
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
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
    options.SwaggerDoc("v1", new()
    {
        Title = "Buscarro API",
        Version = "v1"
    });
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

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(FindAllVehiclesQuery).Assembly));

var buscarroConnectionString = builder.Configuration.GetConnectionString("BuscarroDb");

builder.Services.AddDbContext<BuscarroDbContext>(options =>
    options.UseSqlServer(buscarroConnectionString),
    optionsLifetime: ServiceLifetime.Singleton);
builder.Services.AddDbContextFactory<BuscarroDbContext>(options =>
    options.UseSqlServer(buscarroConnectionString));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IFavoriteVehicleRepository, FavoriteVehicleRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IVehicleProviderCatalogReadService, VehicleProviderCatalogReadService>();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("Jwt"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtOptions.CreateSecurityKey(),
            ValidateLifetime = true,
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

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
