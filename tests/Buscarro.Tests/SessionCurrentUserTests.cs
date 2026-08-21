using Buscarro.Api.Configurations;
using Buscarro.Api.Controllers;
using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using System.Text;

namespace Buscarro.Tests;

public sealed class SessionCurrentUserTests
{
    [Fact]
    public async Task Login_stores_user_id_in_session()
    {
        var userId = Guid.NewGuid();
        var session = new FakeSession();
        var controller = new AuthController(new FakeMediator(new AuthResponse
        {
            User = new UserResponse
            {
                Id = userId,
                Name = "Alex",
                Email = "alex@example.com"
            }
        }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Session = session
                }
            }
        };

        var result = await controller.Login(
            new LoginUserCommand("alex@example.com", "secret"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId.ToString(), session.GetString("UserId"));
    }

    [Fact]
    public void CurrentUserService_reads_user_id_from_session()
    {
        var userId = Guid.NewGuid();
        var session = new FakeSession();
        session.SetString("UserId", userId.ToString());
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                Session = session
            }
        };

        var currentUser = new CurrentUserService(accessor);

        Assert.Equal(userId, currentUser.UserId);
    }

    [Fact]
    public void CurrentUserService_reads_user_id_from_authenticated_claim()
    {
        var userId = Guid.NewGuid();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "Bearer"))
            }
        };

        var currentUser = new CurrentUserService(accessor);

        Assert.Equal(userId, currentUser.UserId);
    }

    private sealed class FakeMediator : IMediator
    {
        private readonly AuthResponse _response;

        public FakeMediator(AuthResponse response)
        {
            _response = response;
        }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult((TResponse)(object)_response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            return Task.CompletedTask;
        }

        public Task<object?> Send(
            object request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<object?>(_response);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            return EmptyStream<TResponse>();
        }

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default)
        {
            return EmptyStream<object?>();
        }

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Task.CompletedTask;
        }

        public Task Publish(
            object notification,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        private static async IAsyncEnumerable<T> EmptyStream<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = [];

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => _values.Keys;

        public void Clear()
        {
            _values.Clear();
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            _values.Remove(key);
        }

        public void Set(string key, byte[] value)
        {
            _values[key] = value;
        }

        public bool TryGetValue(string key, out byte[] value)
        {
            return _values.TryGetValue(key, out value!);
        }

        public string? GetString(string key)
        {
            return TryGetValue(key, out var value)
                ? Encoding.UTF8.GetString(value)
                : null;
        }

        public void SetString(string key, string value)
        {
            Set(key, Encoding.UTF8.GetBytes(value));
        }
    }
}
