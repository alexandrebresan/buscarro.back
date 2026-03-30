using Buscarro.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Buscarro.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PokemonController : ControllerBase
{
    private readonly IMediator _mediator;

    public PokemonController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Busca informacoes de um Pokemon nas duas APIs externas e retorna os dados normalizados.
    /// </summary>
    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPokemonQuery(name), cancellationToken);

        if (result.Count == 0)
        {
            return NotFound($"Nenhuma API retornou dados para o pokemon '{name}'.");
        }

        return Ok(result);
    }
}
