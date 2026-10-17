using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListFuelsQuery
    : IRequest<IReadOnlyCollection<string>>;