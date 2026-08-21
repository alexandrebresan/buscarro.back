using System.Globalization;
using Buscarro.Application.DTOs;

namespace Buscarro.Application.Services;

public sealed class VehicleSearchUrlBuilder
{
    private readonly VehicleSearchCriteriaCanonicalizer _canonicalizer;

    public VehicleSearchUrlBuilder(
        VehicleSearchCriteriaCanonicalizer canonicalizer)
    {
        _canonicalizer = canonicalizer ??
            throw new ArgumentNullException(nameof(canonicalizer));
    }

    public VehicleSearchUrlBuilder()
        : this(new VehicleSearchCriteriaCanonicalizer())
    {
    }

    public string Build(
        VehicleSearchCriteria source,
        VehicleSearchTaxonomy taxonomy)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(taxonomy);

        var criteria = _canonicalizer.Canonicalize(
            source,
            taxonomy);

        var parameters =
            new List<KeyValuePair<string, string>>();

        Add(parameters, "provider", criteria.Providers);
        Add(parameters, "state", criteria.States);
        Add(parameters, "city", criteria.Cities);
        Add(parameters, "manufacturer", criteria.Manufacturers);
        Add(parameters, "model", criteria.Models);
        Add(parameters, "version", criteria.Versions);

        Add(parameters, "priceMin", criteria.PriceMin);
        Add(parameters, "priceMax", criteria.PriceMax);
        Add(parameters, "yearMin", criteria.YearMin);
        Add(parameters, "yearMax", criteria.YearMax);

        Add(parameters, "color", criteria.Colors);
        Add(parameters, "gear", criteria.Gears);
        Add(parameters, "fuel", criteria.Fuels);

        var query = string.Join(
            "&",
            parameters.Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Key)}=" +
                $"{Uri.EscapeDataString(parameter.Value)}"));

        return query.Length == 0
            ? "/api/vehicles"
            : $"/api/vehicles?{query}";
    }

    private static void Add(
        ICollection<KeyValuePair<string, string>> target,
        string name,
        IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            target.Add(new KeyValuePair<string, string>(
                name,
                value));
        }
    }

    private static void Add<T>(
        ICollection<KeyValuePair<string, string>> target,
        string name,
        T? value)
        where T : struct, IFormattable
    {
        if (value.HasValue)
        {
            target.Add(new KeyValuePair<string, string>(
                name,
                value.Value.ToString(
                    null,
                    CultureInfo.InvariantCulture)));
        }
    }
}
