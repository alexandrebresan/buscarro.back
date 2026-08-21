using System.Globalization;
using System.Text;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;

namespace Buscarro.Application.Services;

public sealed class VehicleCatalogSearchCriteriaNormalizer
{
    private const int MaxValues = 20;

    public NormalizedVehicleCatalogSearchCriteria Normalize(VehicleCatalogSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pageSize = ResolvePageSize(request.PageSize, request.Results);
        if (request.Page < 1) throw Invalid("page deve ser maior ou igual a 1.");
        if (pageSize is < 1 or > 50) throw Invalid("pageSize deve estar entre 1 e 50.");
        if (request.Query?.Length > 200) throw Invalid("query excede 200 caracteres.");
        ValidateRange(request.PriceMin, request.PriceMax, "price");
        ValidateRange(request.YearMin, request.YearMax, "year");
        if (request.YearMax > DateTimeOffset.UtcNow.Year + 1) throw Invalid("yearMax excede o ano permitido.");
        var order = NormalizeOptional(request.Order);
        if (order is not null && order is not ("price" or "price-inverse" or "year" or "year-inverse"))
            throw Invalid("order invalida.");

        var cities=NormalizeValues(request.City, 120, "city");
        var colors=NormalizeValues(request.Color, 80, "color");
        var fuels=NormalizeValues(request.Fuel, 80, "fuel");
        var gears=NormalizeValues(request.Gear, 80, "gear");
        RejectNumericProviderCodes(cities,"city");
        RejectNumericProviderCodes(colors,"color");
        RejectNumericProviderCodes(fuels,"fuel");
        RejectNumericProviderCodes(gears,"gear");

        return new(
            NormalizeOptional(request.Query),
            NormalizeValues(request.Provider, 80, "provider"),
            NormalizeValues(request.State, 80, "state"),
            cities,
            NormalizeValues(request.Manufacturer, 120, "manufacturer"),
            NormalizeValues(request.Model, 120, "model"),
            NormalizeValues(request.Version, 160, "version"),
            colors,
            fuels,
            gears,
            request.PriceMin, request.PriceMax, request.YearMin, request.YearMax,
            order, request.Page, pageSize);
    }

    public static string NormalizeText(string value)
    {
        var decomposed = CollapseWhitespace(value.Trim().Normalize(NormalizationForm.FormKC))
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static IReadOnlyList<string> NormalizeValues(IEnumerable<string>? values, int maxLength, string name)
    {
        var raw = values?.ToArray() ?? [];
        if (raw.Length > MaxValues) throw Invalid($"{name} excede {MaxValues} valores.");
        var normalized = raw.Where(v => !string.IsNullOrWhiteSpace(v)).Select(NormalizeText).ToArray();
        if (normalized.Any(v => v.Length > maxLength)) throw Invalid($"{name} excede o limite permitido.");
        return normalized.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
    private static void RejectNumericProviderCodes(IReadOnlyCollection<string> values, string name)
    {
        if (values.Any(value =>
                value.Length > 0 &&
                value.All(char.IsDigit)))
        {
            throw Invalid(
                $"{name} aceita somente nomes, nunca IDs do provider.");
        }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : NormalizeText(value);
    private static string CollapseWhitespace(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static int ResolvePageSize(int? pageSize, int? results)
    {
        if (pageSize.HasValue && results.HasValue && pageSize != results) throw Invalid("pageSize e results devem ser iguais.");
        return pageSize ?? results ?? 20;
    }
    private static void ValidateRange<T>(T? min, T? max, string name) where T : struct, IComparable<T>
    {
        if (min is IComparable<T> minValue && minValue.CompareTo(default) < 0 || max is IComparable<T> maxValue && maxValue.CompareTo(default) < 0)
            throw Invalid($"{name} nao pode ser negativo.");
        if (min.HasValue && max.HasValue && min.Value.CompareTo(max.Value) > 0) throw Invalid($"{name} minimo excede o maximo.");
    }
    private static ValidationException Invalid(string message) => new(message);
}
