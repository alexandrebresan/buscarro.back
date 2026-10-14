using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;

namespace Buscarro.Application.Services;

public sealed class CatalogSyncValidator(TimeProvider timeProvider, CatalogSynchronizationOptions options) : ICatalogSyncValidator
{
    public CatalogItemValidationResult ValidateAndNormalize(CatalogSourceVehicle source)
    {
        var externalId = Clean(source.ExternalId);
        if (externalId is null) return Invalid("missing_external_id");
        if (externalId.Length > 160) return Invalid("external_id_too_long");
        if (string.IsNullOrWhiteSpace(source.Title)) return Invalid("missing_title");
        var maxYear = timeProvider.GetUtcNow().Year + 1;
        if (source.Year is < 0 || source.Year is > 0 and < 1900 || source.Year > maxYear) return Invalid("invalid_year");
        if (source.Mileage is < 0) return Invalid("invalid_mileage");
        if (source.Price is < 0) return Invalid("invalid_price");

        var normalized = source with
        {
            ExternalId = externalId,
            Title = Clean(source.Title), Brand = Clean(source.Brand), Model = Clean(source.Model), Version = Clean(source.Version),
            ManufacturerCode = Clean(source.ManufacturerCode), ModelCode = Clean(source.ModelCode), VersionCode = Clean(source.VersionCode),
            Fuel = Clean(source.Fuel), Transmission = Clean(source.Transmission), Color = Clean(source.Color), City = Clean(source.City), State = Clean(source.State),
            Url = Clean(source.Url), ImageUrl = Clean(source.ImageUrl),
            Year = source.Year, Mileage = source.Mileage, Price = source.Price
        };
        if (normalized.Title!.Length > 300 || TooLong(normalized.Brand, 120) || TooLong(normalized.Model, 120) ||
            TooLong(normalized.Version, 160) || TooLong(normalized.ManufacturerCode, 80) || TooLong(normalized.ModelCode, 80) ||
            TooLong(normalized.VersionCode, 80) || TooLong(normalized.Fuel, 80) || TooLong(normalized.Transmission, 80) ||
            TooLong(normalized.Color, 80) || TooLong(normalized.City, 120) || TooLong(normalized.State, 80)) return Invalid("text_too_long");
        if (TooLong(normalized.Url, 1_000)) return Invalid("url_too_long");
        if (TooLong(normalized.ImageUrl, 1_000)) return Invalid("image_url_too_long");
        return new(true, normalized, null);
    }

    public string? ValidateSnapshot(int validItemCount, int? previousItemCount)
        => validItemCount == 0
            ? "catalog_no_valid_items"
            : previousItemCount is > 0 && validItemCount < previousItemCount * (1m - options.MaximumDropPercentage / 100m)
            ? "catalog_drop_exceeded" : null;

    public static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Regex.Replace(value.Normalize(NormalizationForm.FormKC).Trim(), @"\s+", " ");
    }

    public static string? Normalize(string? value)
    {
        var clean = Clean(value);
        if (clean is null) return null;
        var decomposed = clean.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) builder.Append(character);
        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    private static bool TooLong(string? value, int maximum) => value?.Length > maximum;

    private static CatalogItemValidationResult Invalid(string reason) => new(false, null, reason);
}
