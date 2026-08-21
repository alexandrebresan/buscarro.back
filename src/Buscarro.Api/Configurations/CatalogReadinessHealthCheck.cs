using Buscarro.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Buscarro.Api.Configurations;

public sealed class CatalogReadinessHealthCheck(ICatalogReadinessProbe probe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await probe.CheckAsync(cancellationToken);
        var data = new Dictionary<string, object>();
        if (result.PublicationId is int publicationId) data["publicationId"] = publicationId;
        if (result.AgeSeconds is long ageSeconds) data["ageSeconds"] = ageSeconds;
        if (result.ProviderCount is int providerCount) data["providerCount"] = providerCount;
        return result.IsReady
            ? HealthCheckResult.Healthy(result.Reason, data)
            : HealthCheckResult.Unhealthy(result.Reason, data: data);
    }
}
