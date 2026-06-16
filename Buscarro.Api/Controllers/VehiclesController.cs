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
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(
        IMediator mediator,
        ILogger<VehiclesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> FindAll(
        [FromQuery] VehicleSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new FindAllVehiclesQuery(criteria), cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{externalId:int}")]
    public async Task<IActionResult> GetDetails(
        int externalId,
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
        catch (ProviderAccessDeniedException ex)
        {
            _logger.LogWarning(
                ex,
                "Provider {Provider} recusou acesso aos detalhes do veiculo {ExternalId}.",
                ex.Provider,
                externalId);

            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }
}
