namespace Buscarro.Infrastructure.Providers;

public sealed class AutoDevOptions
{
    public string BaseUrl { get; init; } = "https://api.auto.dev/";
    public string ApiKey { get; init; } = string.Empty;
}
