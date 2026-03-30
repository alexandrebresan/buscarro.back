using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Queries;

public sealed record GetVehicleYearsQuery : IRequest<IReadOnlyCollection<VehicleYearResponse>>;
