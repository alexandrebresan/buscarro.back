using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
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

    [HttpGet("details/{externalId}")]
    public async Task<IActionResult> GetDetails(
        string externalId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new GetVehicleDetailsQuery(externalId), cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
