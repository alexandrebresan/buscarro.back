namespace Buscarro.Application.DTOs;

public sealed record CatalogReadinessProbeResult(
    bool IsReady,
    string Reason,
    int? PublicationId = null,
    long? AgeSeconds = null,
    int? ProviderCount = null);
