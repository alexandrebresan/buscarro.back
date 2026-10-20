using System.Collections.ObjectModel;
using Buscarro.Application.DTOs;

namespace Buscarro.Application.Services;

public sealed class SemanticProfileCatalog
{
    private static readonly IReadOnlyList<SemanticProfileDefinition>
        DefaultDefinitions =
        [
            Group("suv", ["SUV", "SUVs", "utilitário esportivo", "utilitários esportivos"]),
            Group("sedan", ["sedan", "sedans", "sedã", "sedãs"]),
            Group("hatch", ["hatch", "hatches", "hatchback", "hatchbacks"]),
            Group("economic", ["econômico", "econômica", "econômicos", "econômicas", "baixo consumo", "consumo baixo", "não bebe muito"]),
            Group("family", ["família", "famílias", "familiar", "familiares", "para filhos", "cadeirinha"]),
            Group("fun", ["divertido", "divertida", "divertidos", "divertidas", "divertido de dirigir", "boa dirigibilidade", "bom de curva", "gostoso de dirigir"]),
            Group("large", ["grande", "grandes", "porte grande"]),
            new("low_mileage", ["pouco rodado", "pouco rodada", "pouco rodados", "pouco rodadas", "baixa quilometragem"],
                _ => new VehicleSearchCriteria { MileageMax = 30_000 }),
            new("old", ["antigo", "antiga", "antigos", "antigas"],
                clock => new VehicleSearchCriteria { YearMax = clock.GetUtcNow().Year - 20 }),
            new("modern", ["moderno", "moderna", "modernos", "modernas"],
                clock => new VehicleSearchCriteria { YearMin = clock.GetUtcNow().Year - 5 }),
            new(
                "semi_new",
                ["seminovo", "seminova"],
                timeProvider => new VehicleSearchCriteria
                {
                    YearMin = timeProvider.GetUtcNow().Year - 3
                }),
            new(
                "beginner",
                ["iniciante", "primeiro carro", "meu primeiro carro"],
                _ => new VehicleSearchCriteria
                {
                    Gears = ["manual"],
                    PriceMax = 65_000m,
                    Fuels = ["gasolina e álcool"],
                    YearMin = 2014
                })
        ];

    private static SemanticProfileDefinition Group(string code, IReadOnlyList<string> aliases)
        => new(code, aliases, _ => new VehicleSearchCriteria { Profiles = [code] });

    public SemanticProfileCatalog()
        : this(DefaultDefinitions)
    {
    }

    public SemanticProfileCatalog(
        IReadOnlyList<SemanticProfileDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var copies = definitions
            .Select(CopyDefinition)
            .ToArray();

        Validate(copies);
        Definitions = new ReadOnlyCollection<SemanticProfileDefinition>(
            copies);
    }

    public IReadOnlyList<SemanticProfileDefinition> Definitions { get; }

    public SemanticProfileDefinition GetRequired(string code)
    {
        return Definitions.FirstOrDefault(
                   definition => string.Equals(
                       definition.Code,
                       code,
                       StringComparison.Ordinal))
               ?? throw new KeyNotFoundException(
                   $"Semantic profile '{code}' was not found.");
    }

    private static SemanticProfileDefinition CopyDefinition(
        SemanticProfileDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Aliases);
        ArgumentNullException.ThrowIfNull(definition.CreateCriteria);

        return definition with
        {
            Aliases = new ReadOnlyCollection<string>(
                definition.Aliases.ToArray())
        };
    }

    private static void Validate(
        IReadOnlyList<SemanticProfileDefinition> definitions)
    {
        var missingCode = definitions.FirstOrDefault(definition =>
            string.IsNullOrWhiteSpace(definition.Code));
        if (missingCode is not null)
        {
            throw new ArgumentException(
                "Semantic profile code cannot be null, empty or whitespace.",
                nameof(definitions));
        }

        var duplicateCode = definitions
            .GroupBy(definition => definition.Code, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCode is not null)
        {
            throw new ArgumentException(
                $"Semantic profile code '{duplicateCode.Key}' is duplicated.",
                nameof(definitions));
        }

        var aliases = definitions
            .SelectMany(definition => definition.Aliases.Select(alias => new
            {
                definition.Code,
                Alias = alias,
                Normalized = alias is null
                    ? null
                    : SemanticProfileTextNormalizer.Normalize(alias),
                Terms = alias is null
                    ? null
                    : SemanticProfileTextNormalizer.GetTerms(alias)
            }))
            .ToArray();

        var emptyAlias = aliases.FirstOrDefault(alias =>
            alias.Normalized is null ||
            alias.Normalized.Length == 0);
        if (emptyAlias is not null)
        {
            throw new ArgumentException(
                $"Semantic profile '{emptyAlias.Code}' has a null or empty " +
                "alias.",
                nameof(definitions));
        }

        var duplicateAlias = aliases
            .GroupBy(alias => new
            {
                alias.Code,
                alias.Normalized
            })
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateAlias is not null)
        {
            throw new ArgumentException(
                $"Semantic profile '{duplicateAlias.Key.Code}' has a " +
                "duplicated normalized alias.",
                nameof(definitions));
        }

        for (var leftIndex = 0;
             leftIndex < aliases.Length;
             leftIndex++)
        {
            for (var rightIndex = leftIndex + 1;
                 rightIndex < aliases.Length;
                 rightIndex++)
            {
                var left = aliases[leftIndex];
                var right = aliases[rightIndex];
                if (string.Equals(
                        left.Code,
                        right.Code,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (AliasesOverlap(
                        left.Normalized!,
                        left.Terms!,
                        right.Normalized!,
                        right.Terms!))
                {
                    throw new ArgumentException(
                        $"Aliases '{left.Alias}' and '{right.Alias}' " +
                        "overlap across semantic profiles.",
                        nameof(definitions));
                }
            }
        }
    }

    private static bool AliasesOverlap(
        string leftNormalized,
        IReadOnlyList<string> leftTerms,
        string rightNormalized,
        IReadOnlyList<string> rightTerms)
    {
        if (string.Equals(
                leftNormalized,
                rightNormalized,
                StringComparison.Ordinal))
        {
            return true;
        }

        if (ContainsTerms(leftTerms, rightTerms) ||
            ContainsTerms(rightTerms, leftTerms))
        {
            return true;
        }

        var maximumOverlap =
            Math.Min(leftTerms.Count, rightTerms.Count) - 1;
        for (var length = 1; length <= maximumOverlap; length++)
        {
            if (SequencesEqual(
                    leftTerms,
                    leftTerms.Count - length,
                    rightTerms,
                    0,
                    length) ||
                SequencesEqual(
                    rightTerms,
                    rightTerms.Count - length,
                    leftTerms,
                    0,
                    length))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsTerms(
        IReadOnlyList<string> source,
        IReadOnlyList<string> candidate)
    {
        if (candidate.Count > source.Count)
        {
            return false;
        }

        for (var start = 0;
             start <= source.Count - candidate.Count;
             start++)
        {
            if (SequencesEqual(
                    source,
                    start,
                    candidate,
                    0,
                    candidate.Count))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SequencesEqual(
        IReadOnlyList<string> left,
        int leftStart,
        IReadOnlyList<string> right,
        int rightStart,
        int length)
    {
        for (var offset = 0; offset < length; offset++)
        {
            if (!string.Equals(
                    left[leftStart + offset],
                    right[rightStart + offset],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
