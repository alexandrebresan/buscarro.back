using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListVersionsQuery(Guid? ModelId) : IRequest<IReadOnlyCollection<VersionCatalogItemResponse>>;
