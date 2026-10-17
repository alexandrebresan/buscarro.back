using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListTransmissionsQuery
    : IRequest<IReadOnlyCollection<string>>;