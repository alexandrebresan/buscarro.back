using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Queries;

public sealed record ListFavoriteVehiclesQuery : IRequest<IReadOnlyCollection<FavoriteVehicleResponse>>;
