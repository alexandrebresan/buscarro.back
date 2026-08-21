namespace Buscarro.Application.DTOs;

public sealed record CatalogRollbackResult(string Provider,int PublicationId,int CurrentGenerationId,int PreviousGenerationId,DateTimeOffset PublishedAtUtc,int TotalCatalogCount);
