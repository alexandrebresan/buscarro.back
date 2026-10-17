using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListColorsQuery(
    string? Manufacturer = null,
    string? Model = null)
    : IRequest<IReadOnlyCollection<string>>;