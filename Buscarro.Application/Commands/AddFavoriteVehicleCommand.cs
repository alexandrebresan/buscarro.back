using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Commands;

public sealed record AddFavoriteVehicleCommand(
    string ExternalId,
    string Provider,
    string Title,
    string Brand,
    string Model,
    string Version,
    int Year,
    int Mileage,
    string Fuel,
    string Transmission,
    string Color,
    decimal Price,
    string City,
    string State,
    string Url,
    string ImageUrl) : IRequest<FavoriteVehicleResponse>;
