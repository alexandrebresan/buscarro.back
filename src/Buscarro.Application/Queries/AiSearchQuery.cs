using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Queries;

public sealed record AiSearchQuery(AiSearchRequest Request) : IRequest<AiSearchResponse>
{
    public AiSearchQuery() : this(new AiSearchRequest())
    {
    }
}
