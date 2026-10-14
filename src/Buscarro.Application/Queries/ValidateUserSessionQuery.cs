using MediatR;
namespace Buscarro.Application.Queries;
public sealed record ValidateUserSessionQuery(Guid UserId, Guid SessionId) : IRequest<bool>;
