using MediatR;

namespace Buscarro.Application.Queries;

public sealed record GetProviderSynchronizationDueQuery(string Provider, TimeSpan Interval) : IRequest<bool>;
