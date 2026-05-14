using Buscarro.Application.DTOs;
using Buscarro.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Buscarro.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly IMediator _mediator;

    public VehiclesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> FindAll(
        [FromQuery] VehicleSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new FindAllVehiclesQuery(criteria), cancellationToken);
        return Ok(result);
    }
}
