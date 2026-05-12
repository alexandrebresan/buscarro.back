using Buscarro.Application.Commands;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Buscarro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FavoritesController : ControllerBase
{
    private readonly IMediator _mediator;

    public FavoritesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListFavoriteVehiclesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        [FromBody] AddFavoriteVehicleCommand command,
        CancellationToken cancellationToken)
    {
        if (command is null)
        {
            return BadRequest("Requisicao invalida.");
        }

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(List), new { id = result.Id }, result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ConflictException ex)
        {
            return Conflict(ex.Message);
        }
        catch (CurrentUserRequiredException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var removed = await _mediator.Send(new RemoveFavoriteVehicleCommand(id), cancellationToken);
            return removed ? NoContent() : NotFound("Favorito nao encontrado.");
        }
        catch (CurrentUserRequiredException ex)
        {
            return Unauthorized(ex.Message);
        }
    }
}
