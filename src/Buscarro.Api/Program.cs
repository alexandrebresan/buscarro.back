using System.Diagnostics;
using Buscarro.Api.Configurations;
using Buscarro.Api.Middlewares;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
ApiHostComposition.ConfigureServices(builder);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<ApiExceptionMiddleware>();

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

app.UseCors(ApiHostComposition.CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health", CatalogHealthResponseWriter.LiveOptions);
app.MapHealthChecks("/health/live", CatalogHealthResponseWriter.LiveOptions);
app.MapHealthChecks("/health/ready", CatalogHealthResponseWriter.ReadyOptions);
app.MapControllers();

app.Run();
