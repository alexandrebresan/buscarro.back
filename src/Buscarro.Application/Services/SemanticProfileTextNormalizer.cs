using System.Globalization;
using System.Text;

namespace Buscarro.Application.Services;

internal static class SemanticProfileTextNormalizer
{
    public static string Normalize(string value)
    {
        return string.Concat(GetTerms(value));
    }

    public static IReadOnlyList<string> GetTerms(string value)
    {
        var terms = new List<string>();
        var currentTerm = new StringBuilder(value.Length);

        foreach (var sourceRune in value.EnumerateRunes())
        {
            var decomposed = sourceRune
                .ToString()
                .Normalize(NormalizationForm.FormD);

            foreach (var decomposedRune in decomposed.EnumerateRunes())
            {
                if (Rune.GetUnicodeCategory(decomposedRune) ==
                    UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (Rune.IsLetterOrDigit(decomposedRune))
                {
                    currentTerm.Append(
                        Rune.ToLowerInvariant(decomposedRune));
                    continue;
                }

                AddCurrentTerm(terms, currentTerm);
            }
        }

        AddCurrentTerm(terms, currentTerm);
        return terms;
    }

    private static void AddCurrentTerm(
        ICollection<string> terms,
        StringBuilder currentTerm)
    {
        if (currentTerm.Length == 0)
        {
            return;
        }

        terms.Add(currentTerm.ToString());
        currentTerm.Clear();
    }
}
