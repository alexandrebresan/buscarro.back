namespace Buscarro.Infrastructure.Providers;

public sealed class CatalogQueryOptions
{
    public const string SectionName = "CatalogQuery";
    public int CommandTimeoutSeconds { get; set; } = 10;
    public int PageCacheTtlSeconds { get; set; } = 15;
    public int CountCacheTtlSeconds { get; set; } = 30;
    public long CacheSizeLimit { get; set; } = 2000;
}
