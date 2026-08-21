namespace Buscarro.Application.DTOs;

public sealed class CatalogSynchronizationOptions
{
    public int PageSize { get; init; } = 50;
    public int BatchSize { get; init; } = 2_000;
    public int MaxPages { get; init; } = 50_000;
    public int MaxVehicles { get; init; } = 2_000_000;
    public decimal MaximumDropPercentage { get; init; } = 50m;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromHours(2);
    public TimeSpan FailureRecordTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan AbandonedLoadingGrace { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan ReadyRetention { get; init; } = TimeSpan.FromDays(1);
    public TimeSpan FailedRetention { get; init; } = TimeSpan.FromDays(7);
}
