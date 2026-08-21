using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Commands;

public sealed record AddFavoriteVehicleCommand(
    string Provider,
    string ExternalId) : IRequest<FavoriteVehicleResponse>;
