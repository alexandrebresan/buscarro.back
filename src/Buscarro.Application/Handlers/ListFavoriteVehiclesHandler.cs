using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class ListFavoriteVehiclesHandler
    : IRequestHandler<ListFavoriteVehiclesQuery, IReadOnlyCollection<FavoriteVehicleResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFavoriteVehicleRepository _favorites;

    public ListFavoriteVehiclesHandler(
        ICurrentUserService currentUser,
        IFavoriteVehicleRepository favorites)
    {
        _currentUser = currentUser;
        _favorites = favorites;
    }

    public async Task<IReadOnlyCollection<FavoriteVehicleResponse>> Handle(
        ListFavoriteVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new CurrentUserRequiredException();
        var favorites = await _favorites.ListByUserIdAsync(userId, cancellationToken);

        return favorites.Select(ToResponse).ToArray();
    }

    private static FavoriteVehicleResponse ToResponse(FavoriteVehicle favorite)
    {
        return new FavoriteVehicleResponse
        {
            Id = favorite.Id,
            ExternalId = favorite.ExternalId,
            Provider = favorite.Provider,
            Title = favorite.Title,
            Brand = favorite.Brand,
            Model = favorite.Model,
            Version = favorite.Version,
            Year = favorite.Year,
            Mileage = favorite.Mileage,
            Doors = favorite.Doors,
            Armored = favorite.Armored,
            Fuel = favorite.Fuel,
            Transmission = favorite.Transmission,
            Color = favorite.Color,
            Price = favorite.Price,
            City = favorite.City,
            State = favorite.State,
            Url = favorite.Url,
            ImageUrl = favorite.ImageUrl,
            CreatedAtUtc = favorite.CreatedAtUtc
        };
    }
}
