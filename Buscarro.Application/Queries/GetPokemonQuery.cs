using Buscarro.Application.DTOs;
using MediatR;

namespace Buscarro.Application.Queries;

public sealed record GetPokemonQuery(string Name) : IRequest<IReadOnlyCollection<PokemonResponse>>;
