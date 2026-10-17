using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListColorsQuery
    : IRequest<IReadOnlyCollection<string>>;