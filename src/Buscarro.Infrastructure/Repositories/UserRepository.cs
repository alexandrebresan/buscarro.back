using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly BuscarroDbContext _dbContext;

    public UserRepository(BuscarroDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Users
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _dbContext.Database.IsNpgsql()
            ? _dbContext.Users.FirstOrDefaultAsync(user => EF.Property<string>(user, "NormalizedEmail") == normalized, cancellationToken)
            : _dbContext.Users.FirstOrDefaultAsync(user => user.Email.Trim().ToLower() == normalized, cancellationToken);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _dbContext.Database.IsNpgsql()
            ? _dbContext.Users.AnyAsync(user => EF.Property<string>(user, "NormalizedEmail") == normalized, cancellationToken)
            : _dbContext.Users.AnyAsync(user => user.Email.Trim().ToLower() == normalized, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
    }
}
