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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IVehicleProviderCatalogReadService, VehicleProviderCatalogReadService>();
builder.Services.AddHttpContextAccessor();
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
app.UseAuthorization();
app.MapControllers();

app.Run();
