using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Queries;

public sealed record GetVehicleDetailsQuery(string ExternalId) : IRequest<VehicleDetailsResponse>;
