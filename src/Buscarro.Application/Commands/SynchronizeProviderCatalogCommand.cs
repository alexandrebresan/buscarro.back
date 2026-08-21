using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Commands;

public sealed record SynchronizeProviderCatalogCommand(string Provider) : IRequest<CatalogSynchronizationResult>;
