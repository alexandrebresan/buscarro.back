using MediatR;

namespace Buscarro.Application.Commands;

public sealed record RemoveFavoriteVehicleCommand(Guid Id) : IRequest<bool>;
