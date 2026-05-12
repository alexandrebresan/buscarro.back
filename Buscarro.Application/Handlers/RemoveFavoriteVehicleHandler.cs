using Buscarro.Application.Commands;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Interfaces;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class RemoveFavoriteVehicleHandler : IRequestHandler<RemoveFavoriteVehicleCommand, bool>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFavoriteVehicleRepository _favorites;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveFavoriteVehicleHandler(
        ICurrentUserService currentUser,
        IFavoriteVehicleRepository favorites,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _favorites = favorites;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(RemoveFavoriteVehicleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new CurrentUserRequiredException();
        var favorite = await _favorites.GetByIdAndUserIdAsync(request.Id, userId, cancellationToken);

        if (favorite is null)
        {
            return false;
        }

        _favorites.Remove(favorite);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
