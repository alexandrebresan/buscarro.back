using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListVersionsQuery(string? Model, string? Manufacturer = null) : IRequest<IReadOnlyCollection<string>>;
