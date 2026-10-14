using MediatR;
using Buscarro.Application.DTOs;
namespace Buscarro.Application.Commands;
public sealed record CreateGoogleChallengeCommand(string BrowserBinding) : IRequest<GoogleChallengeResponse>;
