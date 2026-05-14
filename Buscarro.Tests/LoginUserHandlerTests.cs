using Buscarro.Application.Commands;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Domain.Entities;
using Buscarro.Domain.Interfaces;

namespace Buscarro.Tests;

public sealed class LoginUserHandlerTests
{
    [Fact]
    public async Task Handle_returns_user_when_email_and_password_match()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Alex",
            Email = "alex@example.com",
            PasswordHash = "hashed-secret"
        };
        var handler = new LoginUserHandler(
            new FakeUserRepository(user),
            new FakePasswordHasherService());

        var response = await handler.Handle(
            new LoginUserCommand("ALEX@EXAMPLE.COM", "secret"),
            CancellationToken.None);

        Assert.Equal(user.Id, response.User.Id);
        Assert.Equal("Alex", response.User.Name);
        Assert.Equal("alex@example.com", response.User.Email);
    }

    [Fact]
    public async Task Handle_rejects_invalid_password()
    {
        var user = new User
        {
            Email = "alex@example.com",
            PasswordHash = "hashed-secret"
        };
        var handler = new LoginUserHandler(
            new FakeUserRepository(user),
            new FakePasswordHasherService());

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            handler.Handle(new LoginUserCommand("alex@example.com", "wrong"), CancellationToken.None));
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly User? _user;

        public FakeUserRepository(User? user)
        {
            _user = user;
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult<User?>(null);
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult(_user?.Email == email ? _user : null);
        }

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
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
}
