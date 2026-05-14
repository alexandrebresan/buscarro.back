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
    private readonly IVehicleCatalogReadService _vehicleCatalogReadService;

    public AddFavoriteVehicleHandler(
        ICurrentUserService currentUser,
        IFavoriteVehicleRepository favorites,
        IUnitOfWork unitOfWork,
        IVehicleCatalogReadService vehicleCatalogReadService)
    {
        _currentUser = currentUser;
        _favorites = favorites;
        _unitOfWork = unitOfWork;
        _vehicleCatalogReadService = vehicleCatalogReadService;
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

        var vehicle = await _vehicleCatalogReadService.FindVehicleByProviderAndExternalIdAsync(
            provider,
            externalId,
            cancellationToken);

        if (vehicle is null)
        {
            throw new NotFoundException("Anuncio nao encontrado para favoritar.");
        }

        var favorite = new FavoriteVehicle
        {
            UserId = userId,
            Provider = NormalizeRequired(vehicle.Provider, "Provider"),
            ExternalId = NormalizeRequired(vehicle.ExternalId, "ExternalId"),
            Title = NormalizeOptional(vehicle.Title),
            Brand = NormalizeOptional(vehicle.Brand),
            Model = NormalizeOptional(vehicle.Model),
            Version = NormalizeOptional(vehicle.Version),
            Year = vehicle.Year,
            Mileage = vehicle.Mileage,
            Doors = vehicle.Doors,
            Armored = vehicle.Armored,
            Fuel = NormalizeOptional(vehicle.Fuel),
            Transmission = NormalizeOptional(vehicle.Transmission),
            Color = NormalizeOptional(vehicle.Color),
            Price = vehicle.Price,
            City = NormalizeOptional(vehicle.City),
            State = NormalizeOptional(vehicle.State),
            Url = NormalizeOptional(vehicle.Url),
            ImageUrl = NormalizeOptional(vehicle.ImageUrl)
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
