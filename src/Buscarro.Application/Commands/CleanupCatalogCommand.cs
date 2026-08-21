using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Commands;

public sealed record CleanupCatalogCommand(
    DateTimeOffset AbandonedBeforeUtc,
    DateTimeOffset ReadyBeforeUtc,
    DateTimeOffset FailedBeforeUtc) : IRequest<CatalogCleanupResult>;
