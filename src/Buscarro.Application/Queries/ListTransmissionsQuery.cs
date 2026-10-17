using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListTransmissionsQuery(
    string? Manufacturer = null,
    string? Model = null)
    : IRequest<IReadOnlyCollection<string>>;