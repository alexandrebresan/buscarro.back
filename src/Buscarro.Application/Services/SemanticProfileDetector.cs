using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;

namespace Buscarro.Application.Services;

public sealed partial class SemanticProfileDetector : ISemanticProfileDetector
{
    private readonly SemanticProfileCatalog _catalog;

    public SemanticProfileDetector(SemanticProfileCatalog catalog)
    {
        _catalog = catalog;
    }

    public SemanticProfileDetectionResult Detect(string prompt)
    {
        if (prompt.Length == 0)
        {
            return new SemanticProfileDetectionResult(
                [],
                string.Empty);
        }

        var normalizedPrompt = NormalizeWithPositions(prompt);
        var candidates = FindCandidates(prompt, normalizedPrompt);
        var selectedMatches = SelectLongestNonOverlapping(candidates);

        if (selectedMatches.Count == 0)
        {
            return new SemanticProfileDetectionResult([], prompt);
        }

        var orderedMatches = selectedMatches
            .OrderBy(match => match.Start)
            .ToArray();
        var profileCodes = orderedMatches
            .Select(match => match.ProfileCode)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new SemanticProfileDetectionResult(
            profileCodes,
            RemoveMatches(prompt, orderedMatches));
    }

    private List<ProfileMatch> FindCandidates(
        string prompt,
        NormalizedText normalizedPrompt)
    {
        var candidates = new List<ProfileMatch>();

        foreach (var definition in _catalog.Definitions)
        {
            foreach (var alias in definition.Aliases)
            {
                var normalizedAlias = Normalize(alias);
                var searchStart = 0;

                while (searchStart < normalizedPrompt.Value.Length)
                {
                    var matchIndex = normalizedPrompt.Value.IndexOf(
                        normalizedAlias,
                        searchStart,
                        StringComparison.Ordinal);
                    if (matchIndex < 0)
                    {
                        break;
                    }

                    var start = normalizedPrompt.OriginalStarts[matchIndex];
                    var normalizedEnd =
                        matchIndex + normalizedAlias.Length - 1;
                    var end = ExtendThroughCombiningMarks(
                        prompt,
                        normalizedPrompt.OriginalEnds[normalizedEnd]);

                    if (HasBoundary(prompt, start, end))
                    {
                        candidates.Add(new ProfileMatch(
                            definition.Code,
                            start,
                            end,
                            normalizedAlias.Length));
                    }

                    searchStart = matchIndex + 1;
                }
            }
        }

        return candidates;
    }

    private static List<ProfileMatch> SelectLongestNonOverlapping(
        IEnumerable<ProfileMatch> candidates)
    {
        var selected = new List<ProfileMatch>();

        foreach (var candidate in candidates
                     .OrderByDescending(match => match.NormalizedLength)
                     .ThenByDescending(match => match.End - match.Start)
                     .ThenBy(match => match.Start))
        {
            var overlaps = selected.Any(existing =>
                candidate.Start < existing.End &&
                existing.Start < candidate.End);
            if (!overlaps)
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    private static string RemoveMatches(
        string prompt,
        IReadOnlyList<ProfileMatch> matches)
    {
        var removed = new bool[prompt.Length];
        foreach (var match in matches)
        {
            for (var index = match.Start; index < match.End; index++)
            {
                removed[index] = true;
            }
        }

        var builder = new StringBuilder(prompt.Length);
        for (var index = 0; index < prompt.Length; index++)
        {
            if (!removed[index])
            {
                builder.Append(prompt[index]);
            }
        }

        var compacted = WhitespaceRegex()
            .Replace(builder.ToString(), " ")
            .Trim();

        return TrimEdgePunctuation(compacted);
    }

    private static string TrimEdgePunctuation(string value)
    {
        var start = 0;
        var end = value.Length;

        while (start < end &&
               (char.IsWhiteSpace(value[start]) ||
                char.IsPunctuation(value[start])))
        {
            start++;
        }

        while (end > start &&
               (char.IsWhiteSpace(value[end - 1]) ||
                char.IsPunctuation(value[end - 1])))
        {
            end--;
        }

        return value[start..end].Trim();
    }

    private static bool HasBoundary(string prompt, int start, int end)
    {
        var hasLeftBoundary = start == 0 ||
                              !TryGetPreviousRune(
                                  prompt,
                                  start,
                                  out var previousRune) ||
                              !Rune.IsLetterOrDigit(previousRune);
        var hasRightBoundary = end == prompt.Length ||
                               !TryGetNextRune(
                                   prompt,
                                   end,
                                   out var nextRune) ||
                               !Rune.IsLetterOrDigit(nextRune);

        return hasLeftBoundary && hasRightBoundary;
    }

    private static int ExtendThroughCombiningMarks(
        string prompt,
        int end)
    {
        while (TryGetNextRune(prompt, end, out var rune) &&
               IsCombiningMark(rune))
        {
            end += rune.Utf16SequenceLength;
        }

        return end;
    }

    private static bool IsCombiningMark(Rune rune)
    {
        return Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark;
    }

    private static bool TryGetPreviousRune(
        string value,
        int end,
        out Rune rune)
    {
        return Rune.DecodeLastFromUtf16(
                   value.AsSpan(0, end),
                   out rune,
                   out _) == OperationStatus.Done;
    }

    private static bool TryGetNextRune(
        string value,
        int start,
        out Rune rune)
    {
        return Rune.DecodeFromUtf16(
                   value.AsSpan(start),
                   out rune,
                   out _) == OperationStatus.Done;
    }

    private static NormalizedText NormalizeWithPositions(string value)
    {
        var normalized = new StringBuilder(value.Length);
        var originalStarts = new List<int>(value.Length);
        var originalEnds = new List<int>(value.Length);
        var originalOffset = 0;

        foreach (var sourceRune in value.EnumerateRunes())
        {
            var sourceEnd =
                originalOffset + sourceRune.Utf16SequenceLength;
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
                    var normalizedRune =
                        Rune.ToLowerInvariant(decomposedRune);
                    normalized.Append(normalizedRune);

                    for (var index = 0;
                         index < normalizedRune.Utf16SequenceLength;
                         index++)
                    {
                        originalStarts.Add(originalOffset);
                        originalEnds.Add(sourceEnd);
                    }
                }
            }

            originalOffset = sourceEnd;
        }

        return new NormalizedText(
            normalized.ToString(),
            originalStarts,
            originalEnds);
    }

    private static string Normalize(string value)
    {
        return SemanticProfileTextNormalizer.Normalize(value);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed record NormalizedText(
        string Value,
        IReadOnlyList<int> OriginalStarts,
        IReadOnlyList<int> OriginalEnds);

    private sealed record ProfileMatch(
        string ProfileCode,
        int Start,
        int End,
        int NormalizedLength);
}
