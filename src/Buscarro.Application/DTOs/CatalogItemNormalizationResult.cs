using System.Text.RegularExpressions;

namespace Buscarro.Application.DTOs;

public sealed class CatalogItemNormalizationResult
{
    private static readonly Regex ReasonPattern = new("^[a-z0-9_]{1,80}$", RegexOptions.CultureInvariant);

    private CatalogItemNormalizationResult(bool isValid, CatalogSourceVehicle? vehicle, string? reason)
    {
        IsValid = isValid;
        Vehicle = vehicle;
        Reason = reason;
    }

    public bool IsValid { get; }
    public CatalogSourceVehicle? Vehicle { get; }
    public string? Reason { get; }

    public static CatalogItemNormalizationResult Valid(CatalogSourceVehicle vehicle)
        => new(true, vehicle ?? throw new ArgumentNullException(nameof(vehicle)), null);

    public static CatalogItemNormalizationResult Invalid(string reason)
        => new(false, null, ReasonPattern.IsMatch(reason ?? string.Empty)
            ? reason
            : throw new ArgumentException("Reason must be a stable sanitized code.", nameof(reason)));
}
