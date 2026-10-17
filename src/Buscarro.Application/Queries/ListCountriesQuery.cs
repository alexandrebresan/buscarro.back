using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListCountriesQuery
    : IRequest<IReadOnlyCollection<string>>;