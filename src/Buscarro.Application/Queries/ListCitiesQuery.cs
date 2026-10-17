using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListCitiesQuery(
    IReadOnlyCollection<string> Countries,
    IReadOnlyCollection<string> States)
    : IRequest<IReadOnlyCollection<string>>;