using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Commands;

public sealed record RollbackCatalogCommand(string Provider,int TargetGenerationId):IRequest<CatalogRollbackResult>;
