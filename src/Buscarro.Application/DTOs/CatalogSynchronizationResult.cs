namespace Buscarro.Application.DTOs;

public sealed record CatalogSynchronizationResult(
    string Provider,
    bool Acquired,
    int? GenerationId,
    int SourceItemCount,
    int ValidItemCount,
    int InvalidItemCount,
    int DuplicateItemCount,
    int PageCount=0);
