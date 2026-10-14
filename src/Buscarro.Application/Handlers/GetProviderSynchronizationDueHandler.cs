using Buscarro.Application.Queries;
using Buscarro.Domain.Interfaces;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class GetProviderSynchronizationDueHandler(
    ICatalogGenerationRepository generations,
    TimeProvider timeProvider) : IRequestHandler<GetProviderSynchronizationDueQuery, bool>
{
    public async Task<bool> Handle(GetProviderSynchronizationDueQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Provider)) throw new ArgumentException("Provider is required.", nameof(request));
        if (request.Interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(request), "Interval must be positive.");
        var current = await generations.GetCurrentByProviderAsync(request.Provider.Trim(), cancellationToken);
        if (current?.ActivatedAtUtc is not DateTimeOffset activatedAt) return true;
        var now = timeProvider.GetUtcNow();
        return now >= activatedAt && now - activatedAt >= request.Interval;
    }
}
