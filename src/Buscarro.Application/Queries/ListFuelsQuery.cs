using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListFuelsQuery(
    string? Manufacturer = null,
    string? Model = null)
    : IRequest<IReadOnlyCollection<string>>;