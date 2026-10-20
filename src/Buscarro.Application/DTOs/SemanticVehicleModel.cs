namespace Buscarro.Application.DTOs;

public sealed record SemanticVehicleModel(string Brand, string Model, IReadOnlyList<string> Profiles);
