namespace Buscarro.Application.Services;

public sealed class VehicleSearchPromptContentAnalyzer
{
    private static readonly HashSet<string> NeutralTerms =
        new(StringComparer.Ordinal)
        {
            "quero",
            "queria",
            "gostaria",
            "procuro",
            "busco",
            "preciso",
            "o",
            "a",
            "os",
            "as",
            "um",
            "uma",
            "uns",
            "umas",
            "de",
            "do",
            "da",
            "dos",
            "das",
            "em",
            "no",
            "na",
            "nos",
            "nas",
            "por",
            "pelo",
            "pela",
            "pelos",
            "pelas",
            "para",
            "pra",
            "pro",
            "com",
            "sem",
            "e",
            "ou",
            "que",
            "carro",
            "carros",
            "veiculo",
            "veiculos"
        };

    public bool HasMeaningfulTerms(string sanitizedPrompt)
    {
        ArgumentNullException.ThrowIfNull(sanitizedPrompt);

        return SemanticProfileTextNormalizer
            .GetTerms(sanitizedPrompt)
            .Any(term => !NeutralTerms.Contains(term));
    }
}
