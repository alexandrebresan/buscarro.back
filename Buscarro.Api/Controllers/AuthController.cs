using Buscarro.Application.Commands;
using Buscarro.Application.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Buscarro.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        if (command is null)
        {
            return BadRequest("Requisicao invalida.");
        }

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Created(string.Empty, result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginUserCommand command,
        CancellationToken cancellationToken)
    {
        if (command is null)
        {
            return BadRequest("Requisicao invalida.");
        }

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (AuthenticationFailedException ex)
        {
            return Unauthorized(ex.Message);
        }
    }
}
