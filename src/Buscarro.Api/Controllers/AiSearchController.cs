using Buscarro.Application.DTOs;
using Buscarro.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Buscarro.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiSearchController : ControllerBase
{
    private readonly IMediator _mediator;

    public AiSearchController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> AiSearch(
        [FromBody] AiSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AiSearchQuery(request), cancellationToken);
        return Ok(result);
    }

}
