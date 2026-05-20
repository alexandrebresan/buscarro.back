namespace Buscarro.Infrastructure.Providers;

public sealed class CarzinCatalogOptions
{
    public string ManufacturersPath { get; set; } = string.Empty;
    public string ModelsPath { get; set; } = string.Empty;
    public string VersionsPath { get; set; } = string.Empty;
    public int RequestDelayMilliseconds { get; set; } = 1000;
    public int MaxRetryAttempts { get; set; } = 12;
    public int RetryBaseDelaySeconds { get; set; } = 5;
    public int RetryMaxDelaySeconds { get; set; } = 120;
}
