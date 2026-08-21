using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Services;
using Buscarro.Infrastructure.Contracts;

namespace Buscarro.Infrastructure.Services;

public sealed class OpenAiVehicleSearchCriteriaMapper(
    VehicleSearchCriteriaCanonicalizer canonicalizer)
{
    public VehicleSearchCriteria Map(
        OpenAiVehicleSearchCriteriaContract contract,
        VehicleSearchTaxonomy taxonomy)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(taxonomy);

        RejectNumericValues(
            contract.Providers,
            "providers");

        RejectNumericValues(
            contract.States,
            "states");

        RejectNumericValues(
            contract.Cities,
            "cities");

        RejectNumericValues(
            contract.Manufacturers,
            "manufacturers");

        RejectNumericValues(
            contract.Colors,
            "colors");

        RejectNumericValues(
            contract.Gears,
            "gears");

        RejectNumericValues(
            contract.Fuels,
            "fuels");

        var criteria = new VehicleSearchCriteria
        {
            Providers = Copy(contract.Providers),
            States = Copy(contract.States),
            Cities = Copy(contract.Cities),
            Manufacturers = Copy(contract.Manufacturers),
            Models = Copy(contract.Models),
            Versions = Copy(contract.Versions),
            Colors = Copy(contract.Colors),
            Gears = Copy(contract.Gears),
            Fuels = Copy(contract.Fuels),
            PriceMin = contract.PriceMin,
            PriceMax = contract.PriceMax,
            YearMin = contract.YearMin,
            YearMax = contract.YearMax
        };

        return canonicalizer.Canonicalize(
            criteria,
            taxonomy);
    }

    private static List<string> Copy(
        IEnumerable<string>? values)
    {
        return values?.ToList() ?? [];
    }

    private static void RejectNumericValues(
        IEnumerable<string>? values,
        string name)
    {
        var containsNumericValue =
            values?.Any(value =>
                !string.IsNullOrWhiteSpace(value) &&
                value.Trim().All(char.IsDigit)) == true;

        if (containsNumericValue)
        {
            throw new ValidationException(
                $"{name} nao aceita IDs numericos produzidos pela IA.");
        }
    }
}