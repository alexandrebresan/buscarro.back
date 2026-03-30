using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Queries;

public sealed record GetVehicleBrandsQuery : IRequest<IReadOnlyCollection<VehicleBrandResponse>>;
