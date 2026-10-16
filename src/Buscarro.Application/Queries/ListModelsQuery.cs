using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListModelsQuery(string? Manufacturer = null) : IRequest<IReadOnlyCollection<string>>;
