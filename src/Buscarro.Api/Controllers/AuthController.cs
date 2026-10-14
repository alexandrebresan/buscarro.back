using System.Security.Claims;
using Buscarro.Api.Configurations;
using Buscarro.Application.Commands;
using Buscarro.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Buscarro.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ServiceFilter(typeof(AuthTransportFilter))]
public sealed class AuthController(IMediator mediator, AuthAntiforgeryService antiforgery) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf() => Ok(new { token = antiforgery.GetToken(HttpContext) });

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        WriteRefreshCookie(result);
        return Created(string.Empty, result.Response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginUserCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        WriteRefreshCookie(result);
        return Ok(result.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RefreshSessionCommand(Request.Cookies[AuthCookies.RefreshName]), cancellationToken);
        WriteRefreshCookie(result);
        return Ok(result.Response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await mediator.Send(new LogoutSessionCommand(Request.Cookies[AuthCookies.RefreshName]), cancellationToken);
        AuthCookies.ClearRefresh(HttpContext);
        return NoContent();
    }

    [HttpPost("google/challenge")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleChallenge(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new CreateGoogleChallengeCommand(AuthCookies.GetOrCreateBrowserBinding(HttpContext)), cancellationToken));

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> Google([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GoogleLoginCommand(request.IdToken, request.ChallengeId,
            Request.Cookies[AuthCookies.BrowserName] ?? string.Empty, request.Password, request.DisplayName), cancellationToken);
        WriteRefreshCookie(result);
        return Ok(result.Response);
    }

    [HttpPost("password/reactivate")]
    [Authorize]
    public async Task<IActionResult> ReactivatePassword([FromBody] ReactivatePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var sessionId = Guid.Parse(User.FindFirstValue("sid")!);
        await mediator.Send(new ReactivatePasswordCommand(userId, sessionId, request.Password), cancellationToken);
        return NoContent();
    }

    private void WriteRefreshCookie(SessionResult result)
        => AuthCookies.SetRefresh(HttpContext, result.RefreshToken, result.Response.SessionExpiresAtUtc);
}
