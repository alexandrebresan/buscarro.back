using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListStatesQuery(
    IReadOnlyCollection<string> Countries)
    : IRequest<IReadOnlyCollection<string>>;