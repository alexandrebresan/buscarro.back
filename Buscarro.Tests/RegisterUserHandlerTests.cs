using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;

namespace Buscarro.Tests;

public sealed class RegisterUserHandlerTests
{
    [Fact]
    public async Task Handle_inserts_user_with_hashed_password_and_returns_user_without_token()
    {
        var users = new FakeUserRepository();
        var passwordHasher = new FakePasswordHasherService();
        var handler = new RegisterUserHandler(
            users,
            passwordHasher,
            new FakeUnitOfWork());

        var response = await handler.Handle(
            new RegisterUserCommand("Alex", "ALEX@EXAMPLE.COM", "secret"),
            CancellationToken.None);

        Assert.NotNull(users.Added);
        Assert.Equal("alex@example.com", users.Added.Email);
        Assert.Equal("hashed-secret", users.Added.PasswordHash);
        Assert.Equal(users.Added.Id, response.User.Id);
        Assert.Equal("Alex", response.User.Name);
        Assert.Equal("alex@example.com", response.User.Email);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? Added { get; private set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult<User?>(null);
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult<User?>(null);
        }

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            Added = user;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasherService : IPasswordHasherService
    {
        public string HashPassword(User user, string password)
        {
            return $"hashed-{password}";
        }

        public bool VerifyPassword(User user, string password)
        {
            return user.PasswordHash == $"hashed-{password}";
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(1);
        }
    }
}
