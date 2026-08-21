using Buscarro.Domain.Entities;

namespace Buscarro.Application.DTOs;

public sealed record CatalogItemValidationResult(bool IsValid, CatalogSourceVehicle? Vehicle, string? Reason);
