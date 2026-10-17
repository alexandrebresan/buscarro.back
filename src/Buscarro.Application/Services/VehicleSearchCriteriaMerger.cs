using Buscarro.Application.DTOs;

namespace Buscarro.Application.Services;

public sealed class VehicleSearchCriteriaMerger
{
    private readonly SemanticProfileCatalog _profiles;
    private readonly TimeProvider _timeProvider;
    private readonly VehicleSearchCriteriaCanonicalizer _canonicalizer;

    public VehicleSearchCriteriaMerger(
        SemanticProfileCatalog profiles,
        TimeProvider timeProvider,
        VehicleSearchCriteriaCanonicalizer canonicalizer)
    {
        _profiles = profiles ??
            throw new ArgumentNullException(
                nameof(profiles));

        _timeProvider = timeProvider ??
            throw new ArgumentNullException(
                nameof(timeProvider));

        _canonicalizer = canonicalizer ??
            throw new ArgumentNullException(
                nameof(canonicalizer));
    }

    public VehicleSearchCriteriaMerger(
        SemanticProfileCatalog profiles,
        TimeProvider timeProvider)
        : this(
            profiles,
            timeProvider,
            new VehicleSearchCriteriaCanonicalizer())
    {
    }

    public VehicleSearchCriteria Merge(
        VehicleSearchCriteria explicitCriteria,
        IReadOnlyList<string> profileCodes,
        VehicleSearchTaxonomy taxonomy)
    {
        ArgumentNullException.ThrowIfNull(
            explicitCriteria);

        ArgumentNullException.ThrowIfNull(
            profileCodes);

        ArgumentNullException.ThrowIfNull(
            taxonomy);

        var result =
            _canonicalizer.Canonicalize(
                explicitCriteria,
                taxonomy);

        foreach (var code in profileCodes)
        {
            var definition =
                _profiles.GetRequired(code);

            var createdCriteria =
                definition.CreateCriteria(
                    _timeProvider) ??
                throw new InvalidOperationException(
                    $"Semantic profile '{code}' returned no criteria.");

            var profile =
                _canonicalizer.Canonicalize(
                    createdCriteria,
                    taxonomy);

            result.Providers = Fill(
                result.Providers,
                profile.Providers);

            result.Countries = Fill(
                result.Countries,
                profile.Countries);

            result.States = Fill(
                result.States,
                profile.States);

            result.Cities = Fill(
                result.Cities,
                profile.Cities);

            result.Manufacturers = Fill(
                result.Manufacturers,
                profile.Manufacturers);

            result.Models = Fill(
                result.Models,
                profile.Models);

            result.Versions = Fill(
                result.Versions,
                profile.Versions);

            result.Colors = Fill(
                result.Colors,
                profile.Colors);

            result.Gears = Fill(
                result.Gears,
                profile.Gears);

            result.Fuels = Fill(
                result.Fuels,
                profile.Fuels);

            result.Query = FillText(
                result.Query,
                profile.Query);

            result.Order = FillText(
                result.Order,
                profile.Order);

            result.PriceMin ??=
                profile.PriceMin;

            result.PriceMax ??=
                profile.PriceMax;

            result.YearMin ??=
                profile.YearMin;

            result.YearMax ??=
                profile.YearMax;
        }

        return _canonicalizer.Canonicalize(
            result,
            taxonomy);
    }

    private static List<string> Fill(
        List<string> target,
        List<string> source)
    {
        return target.Count == 0 &&
               source.Count > 0
            ? new List<string>(source)
            : target;
    }

    private static string? FillText(
        string? current,
        string? fallback)
    {
        return string.IsNullOrWhiteSpace(current)
            ? fallback
            : current;
    }
}