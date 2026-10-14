using MediatR;
using Buscarro.Application.DTOs;
namespace Buscarro.Application.Commands;
public sealed record CleanupAuthSessionsCommand() : IRequest<Unit>;
