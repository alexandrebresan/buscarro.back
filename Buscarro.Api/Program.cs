using System.Diagnostics;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using Buscarro.Infrastructure.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
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

builder.Services.AddHttpClient<PokeApiProvider>();
builder.Services.AddHttpClient<PokeDevsProvider>();

builder.Services.AddTransient<IPokemonApiService>(serviceProvider =>
    serviceProvider.GetRequiredService<PokeApiProvider>());
builder.Services.AddTransient<IPokemonApiService>(serviceProvider =>
    serviceProvider.GetRequiredService<PokeDevsProvider>());

builder.Services.AddTransient<IVehicleProvider, OlxVehicleProvider>();
builder.Services.AddTransient<IVehicleProvider, WebMotorsVehicleProvider>();
builder.Services.AddTransient<IVehicleProvider, ICarrosVehicleProvider>();

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

app.MapControllers();

app.Run();
