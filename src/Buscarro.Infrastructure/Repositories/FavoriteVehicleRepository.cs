using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Infrastructure.Repositories;

public sealed class FavoriteVehicleRepository : IFavoriteVehicleRepository
{
    private readonly BuscarroDbContext _dbContext;

    public FavoriteVehicleRepository(BuscarroDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<FavoriteVehicle>> ListByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.FavoriteVehicles
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId)
            .OrderByDescending(favorite => favorite.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);
    }

    public Task<FavoriteVehicle?> GetByIdAndUserIdAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _dbContext.FavoriteVehicles
            .FirstOrDefaultAsync(favorite => favorite.Id == id && favorite.UserId == userId, cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Guid userId,
        string provider,
        string externalId,
        CancellationToken cancellationToken)
    {
        return _dbContext.FavoriteVehicles.AnyAsync(
            favorite =>
                favorite.UserId == userId &&
                favorite.Provider == provider &&
                favorite.ExternalId == externalId,
            cancellationToken);
    }

    public async Task AddAsync(FavoriteVehicle favorite, CancellationToken cancellationToken)
    {
        await _dbContext.FavoriteVehicles.AddAsync(favorite, cancellationToken);
    }

    public void Remove(FavoriteVehicle favorite)
    {
        _dbContext.FavoriteVehicles.Remove(favorite);
    }
}
