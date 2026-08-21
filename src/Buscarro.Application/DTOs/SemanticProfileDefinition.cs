namespace Buscarro.Application.DTOs;

public sealed record SemanticProfileDefinition(
    string Code,
    IReadOnlyList<string> Aliases,
    Func<TimeProvider, VehicleSearchCriteria> CreateCriteria);
