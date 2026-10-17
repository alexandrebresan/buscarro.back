using Buscarro.Application.Exceptions;
using Buscarro.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;

namespace Buscarro.Api.Controllers;

[ApiController]
[Route("api/general")]
public class GeneralController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<GeneralController> _logger;

    public GeneralController(
        IMediator mediator,
        ILogger<GeneralController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("countries")]
    public async Task<IActionResult> ListCountries(
        CancellationToken cancellationToken)
    {
        return await SendCatalogQueryAsync(
            new ListCountriesQuery(),
            cancellationToken);
    }

    [HttpGet("states")]
    public async Task<IActionResult> ListStates(
        [FromQuery] List<string>? country,
        CancellationToken cancellationToken)
    {
        return await SendCatalogQueryAsync(
            new ListStatesQuery(
                country ?? []),
            cancellationToken);
    }

    [HttpGet("cities")]
    public async Task<IActionResult> ListCities(
        [FromQuery] List<string>? country,
        [FromQuery] List<string>? state,
        CancellationToken cancellationToken)
    {
        return await SendCatalogQueryAsync(
            new ListCitiesQuery(
                country ?? [],
                state ?? []),
            cancellationToken);
    }

    [HttpGet("manufacturers")]
    public async Task<IActionResult> ListManufacturers(
        CancellationToken cancellationToken)
    {
        return await SendCatalogQueryAsync(
            new ListManufacturersQuery(),
            cancellationToken);
    }

    [HttpGet("models")]
    public async Task<IActionResult> ListModels(
        [FromQuery] string? manufacturer,
        CancellationToken cancellationToken)
    {
        return await SendCatalogQueryAsync(
            new ListModelsQuery(
                manufacturer),
            cancellationToken);
    }

    [HttpGet("versions")]
    public async Task<IActionResult> ListVersions(
        [FromQuery] string? model,
        [FromQuery] string? manufacturer,
        CancellationToken cancellationToken)
    {
        return await SendCatalogQueryAsync(
            new ListVersionsQuery(
                model,
                manufacturer),
            cancellationToken);
    }

    [HttpGet("fuels")]
    public async Task<IActionResult> ListFuels(
        CancellationToken cancellationToken,
        [FromQuery] string? manufacturer = null,
        [FromQuery] string? model = null)
    {
        return await SendCatalogQueryAsync(
            new ListFuelsQuery(
                manufacturer,
                model),
            cancellationToken);
    }

    [HttpGet("transmissions")]
    public async Task<IActionResult> ListTransmissions(
        CancellationToken cancellationToken,
        [FromQuery] string? manufacturer = null,
        [FromQuery] string? model = null)
    {
        return await SendCatalogQueryAsync(
            new ListTransmissionsQuery(
                manufacturer,
                model),
            cancellationToken);
    }

    [HttpGet("colors")]
    public async Task<IActionResult> ListColors(
        CancellationToken cancellationToken,
        [FromQuery] string? manufacturer = null,
        [FromQuery] string? model = null)
    {
        return await SendCatalogQueryAsync(
            new ListColorsQuery(
                manufacturer,
                model),
            cancellationToken);
    }

    private async Task<IActionResult>
        SendCatalogQueryAsync<TResponse>(
            IRequest<TResponse> query,
            CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _mediator.Send(
                    query,
                    cancellationToken);

            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(
                ex.Message);
        }
        catch (RetryLimitExceededException ex)
        {
            _logger.LogError(
                ex,
                "Erro ao consultar o catalogo geral no banco de dados.");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Banco de dados indisponivel.");
        }
    }
}