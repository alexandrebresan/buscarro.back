using Buscarro.Application.Services;

namespace Buscarro.Infrastructure.Providers;

public static class ProviderCountryCatalog
{
    private sealed record Definition(
        string Provider,
        string Country,
        IReadOnlyCollection<string> Aliases);

    private static readonly IReadOnlyCollection<Definition> Definitions =
    [
        new(
            "Carzin",
            "Brasil",
            ["Brasil", "Brazil", "BR"]),
        new(
            "AutoDev",
            "Estados Unidos",
            ["Estados Unidos", "EUA", "USA", "United States", "US"])
    ];

    public static IReadOnlyCollection<string> CountriesForProviders(
        IEnumerable<string> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var providerSet = providers
            .Where(provider => !string.IsNullOrWhiteSpace(provider))
            .Select(provider => provider.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Definitions
            .Where(definition => providerSet.Contains(definition.Provider))
            .Select(definition => definition.Country)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string CanonicalizeCountry(string country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        var normalized = VehicleCatalogSearchCriteriaNormalizer.NormalizeText(country);

        var definition = Definitions.FirstOrDefault(item =>
            item.Aliases
                .Select(VehicleCatalogSearchCriteriaNormalizer.NormalizeText)
                .Contains(normalized, StringComparer.Ordinal));

        return definition?.Country ?? country.Trim();
    }

    public static IReadOnlyCollection<string> CanonicalizeCountries(
        IEnumerable<string>? countries)
    {
        return (countries ?? [])
            .Where(country => !string.IsNullOrWhiteSpace(country))
            .Select(CanonicalizeCountry)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyCollection<string> ProvidersForCountries(
        IEnumerable<string>? countries)
    {
        var requested = (countries ?? [])
            .Where(country => !string.IsNullOrWhiteSpace(country))
            .Select(VehicleCatalogSearchCriteriaNormalizer.NormalizeText)
            .ToHashSet(StringComparer.Ordinal);

        if (requested.Count == 0)
        {
            return [];
        }

        return Definitions
            .Where(definition => definition.Aliases
                .Select(VehicleCatalogSearchCriteriaNormalizer.NormalizeText)
                .Any(requested.Contains))
            .Select(definition => definition.Provider)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool ProviderMatchesCountries(
        string provider,
        IReadOnlyCollection<string> countries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentNullException.ThrowIfNull(countries);

        if (countries.Count == 0)
        {
            return true;
        }

        var allowedProviders = ProvidersForCountries(countries);

        return allowedProviders.Contains(
            provider,
            StringComparer.OrdinalIgnoreCase);
    }
}