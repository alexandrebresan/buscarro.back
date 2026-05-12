using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class AddFavoriteVehicleHandler
    : IRequestHandler<AddFavoriteVehicleCommand, FavoriteVehicleResponse>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFavoriteVehicleRepository _favorites;
    private readonly IUnitOfWork _unitOfWork;

    public AddFavoriteVehicleHandler(
        ICurrentUserService currentUser,
        IFavoriteVehicleRepository favorites,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _favorites = favorites;
        _unitOfWork = unitOfWork;
    }

    public async Task<FavoriteVehicleResponse> Handle(
        AddFavoriteVehicleCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new CurrentUserRequiredException();
        var provider = NormalizeRequired(request.Provider, "Provider");
        var externalId = NormalizeRequired(request.ExternalId, "ExternalId");

        if (await _favorites.ExistsAsync(userId, provider, externalId, cancellationToken))
        {
            throw new ConflictException("Este veiculo ja esta nos favoritos do usuario.");
        }

        var favorite = new FavoriteVehicle
        {
            UserId = userId,
            Provider = provider,
            ExternalId = externalId,
            Title = NormalizeOptional(request.Title),
            Brand = NormalizeOptional(request.Brand),
            Model = NormalizeOptional(request.Model),
            Version = NormalizeOptional(request.Version),
            Year = request.Year,
            Mileage = request.Mileage,
            Fuel = NormalizeOptional(request.Fuel),
            Transmission = NormalizeOptional(request.Transmission),
            Color = NormalizeOptional(request.Color),
            Price = request.Price,
            City = NormalizeOptional(request.City),
            State = NormalizeOptional(request.State),
            Url = NormalizeOptional(request.Url),
            ImageUrl = NormalizeOptional(request.ImageUrl)
        };

        await _favorites.AddAsync(favorite, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(favorite);
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

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{fieldName} e obrigatorio.");
        }

        return value.Trim();
    }

    private static string NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
