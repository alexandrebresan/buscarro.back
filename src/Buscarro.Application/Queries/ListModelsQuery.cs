using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListModelsQuery(Guid? ManufacturerId) : IRequest<IReadOnlyCollection<ModelCatalogItemResponse>>;
