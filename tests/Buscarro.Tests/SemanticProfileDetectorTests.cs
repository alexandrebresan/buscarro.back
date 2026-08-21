using Buscarro.Application.DTOs;
using Buscarro.Application.Services;

namespace Buscarro.Tests;

public sealed class SemanticProfileDetectorTests
{
    private readonly SemanticProfileDetector _detector =
        new(new SemanticProfileCatalog());

    [Theory]
    [InlineData("seminovo", "semi_new")]
    [InlineData("semi-novo", "semi_new")]
    [InlineData("seminova", "semi_new")]
    [InlineData("semi-nova", "semi_new")]
    [InlineData("iniciante", "beginner")]
    [InlineData("primeiro carro", "beginner")]
    [InlineData("meu primeiro carro", "beginner")]
    public void Detect_recognizes_each_alias(string prompt, string expectedCode)
    {
        var result = _detector.Detect(prompt);

        Assert.Equal([expectedCode], result.ProfileCodes);
        Assert.Equal(string.Empty, result.SanitizedPrompt);
    }

    [Fact]
    public void Detect_ignores_case_accents_and_equivalent_separators()
    {
        var result = _detector.Detect("Quero um SEMI NÔVO para INICIÁNTE");

        Assert.Equal(["semi_new", "beginner"], result.ProfileCodes);
        Assert.Equal("Quero um para", result.SanitizedPrompt);
    }

    [Fact]
    public void Detect_preserves_first_mention_order_and_deduplicates_profiles()
    {
        var result = _detector.Detect(
            "Iniciante, seminovo e meu primeiro carro; semi-nova.");

        Assert.Equal(["beginner", "semi_new"], result.ProfileCodes);
        Assert.Equal("e", result.SanitizedPrompt);
    }

    [Fact]
    public void Detect_prefers_longest_alias_when_matches_overlap()
    {
        var result = _detector.Detect("Meu primeiro carro automático");

        Assert.Equal(["beginner"], result.ProfileCodes);
        Assert.Equal("automático", result.SanitizedPrompt);
    }

    [Theory]
    [InlineData("seminovos")]
    [InlineData("superiniciante")]
    [InlineData("primeiro carros")]
    public void Detect_does_not_match_alias_inside_another_word(string prompt)
    {
        var result = _detector.Detect(prompt);

        Assert.Empty(result.ProfileCodes);
        Assert.Equal(prompt, result.SanitizedPrompt);
    }

    [Theory]
    [InlineData("\U00010400seminovo")]
    [InlineData("seminovo\U00010400")]
    public void Detect_does_not_treat_supplementary_letters_as_boundaries(
        string prompt)
    {
        var result = _detector.Detect(prompt);

        Assert.Empty(result.ProfileCodes);
        Assert.Equal(prompt, result.SanitizedPrompt);
    }

    [Fact]
    public void Detect_preserves_original_text_accents_case_and_model_hyphens()
    {
        var result = _detector.Detect(
            "Citroën C4-Cactus BRANCO, seminovo");

        Assert.Equal(["semi_new"], result.ProfileCodes);
        Assert.Equal(
            "Citroën C4-Cactus BRANCO",
            result.SanitizedPrompt);
    }

    [Fact]
    public void Detect_removes_final_combining_marks_from_an_nfd_alias()
    {
        var result = _detector.Detect(
            "Honda iniciante\u0301");

        Assert.Equal(["beginner"], result.ProfileCodes);
        Assert.Equal("Honda", result.SanitizedPrompt);
    }

    [Fact]
    public void Detect_preserves_emoji_and_does_not_throw()
    {
        var result = _detector.Detect(
            "quero um carro 🚗 seminovo");

        Assert.Equal(["semi_new"], result.ProfileCodes);
        Assert.Equal(
            "quero um carro 🚗",
            result.SanitizedPrompt);
    }

    [Fact]
    public void Detect_removes_all_occurrences_and_cleans_orphan_edge_punctuation()
    {
        var result = _detector.Detect(
            "--- seminovo, Honda Civic, semi-novo!!!");

        Assert.Equal(["semi_new"], result.ProfileCodes);
        Assert.Equal("Honda Civic", result.SanitizedPrompt);
    }

    [Fact]
    public void Detect_returns_empty_result_for_empty_prompt()
    {
        var result = _detector.Detect(string.Empty);

        Assert.Empty(result.ProfileCodes);
        Assert.Equal(string.Empty, result.SanitizedPrompt);
    }

    [Fact]
    public void Detect_uses_the_injected_catalog()
    {
        var catalog = new SemanticProfileCatalog(
        [
            CreateDefinition("electric", ["elétrico"])
        ]);
        var detector = new SemanticProfileDetector(catalog);

        var result = detector.Detect("Quero um ELÉTRICO branco");

        Assert.Equal(["electric"], result.ProfileCodes);
        Assert.Equal("Quero um branco", result.SanitizedPrompt);
    }

    private static SemanticProfileDefinition CreateDefinition(
        string code,
        IReadOnlyList<string> aliases)
    {
        return new SemanticProfileDefinition(
            code,
            aliases,
            _ => new VehicleSearchCriteria());
    }
}

public sealed class SemanticProfileCatalogTests
{
    [Fact]
    public void Semi_new_uses_utc_year_minus_three()
    {
        var profile =
            new SemanticProfileCatalog().GetRequired("semi_new");

        var criteria = profile.CreateCriteria(
            new FixedTimeProvider(new DateTimeOffset(
                2026,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero)));

        Assert.Equal(2023, criteria.YearMin);
        Assert.Empty(criteria.Gears);
        Assert.Null(criteria.PriceMax);
        Assert.Empty(criteria.Fuels);
    }

    [Fact]
    public void Beginner_contains_the_approved_filters()
    {
        var profile =
            new SemanticProfileCatalog().GetRequired("beginner");

        var criteria = profile.CreateCriteria(TimeProvider.System);

        Assert.Equal(["manual"], criteria.Gears);
        Assert.Equal(65_000m, criteria.PriceMax);
        Assert.Equal(["gasolina e álcool"], criteria.Fuels);
        Assert.Equal(2014, criteria.YearMin);
    }

    [Fact]
    public void Constructor_takes_defensive_read_only_copies()
    {
        var aliases = new List<string> { "original" };
        var definitions = new List<SemanticProfileDefinition>
        {
            CreateDefinition("custom", aliases)
        };
        var catalog = new SemanticProfileCatalog(definitions);

        aliases[0] = "alterado";
        definitions.Clear();

        var stored = catalog.GetRequired("custom");
        Assert.Equal(["original"], stored.Aliases);
        Assert.True(
            Assert.IsAssignableFrom<IList<string>>(stored.Aliases)
                .IsReadOnly);
        Assert.True(
            Assert.IsAssignableFrom<IList<SemanticProfileDefinition>>(
                    catalog.Definitions)
                .IsReadOnly);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<string>>(stored.Aliases)
                .Add("novo"));
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<SemanticProfileDefinition>>(
                    catalog.Definitions)
                .Clear());
    }

    [Fact]
    public void Constructor_rejects_duplicate_codes()
    {
        var definitions = new[]
        {
            CreateDefinition("duplicate", ["um"]),
            CreateDefinition("duplicate", ["dois"])
        };

        Assert.Throws<ArgumentException>(() =>
            new SemanticProfileCatalog(definitions));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_missing_profile_code(string? code)
    {
        var definitions = new[]
        {
            CreateDefinition(code!, ["válido"])
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            new SemanticProfileCatalog(definitions));

        Assert.Contains(
            "code",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    public void Constructor_rejects_empty_normalized_alias(string alias)
    {
        var definitions = new[]
        {
            CreateDefinition("invalid", [alias])
        };

        Assert.Throws<ArgumentException>(() =>
            new SemanticProfileCatalog(definitions));
    }

    [Fact]
    public void Constructor_rejects_null_alias_with_a_clear_error()
    {
        var definitions = new[]
        {
            CreateDefinition("invalid", [null!])
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            new SemanticProfileCatalog(definitions));

        Assert.Contains(
            "alias",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_rejects_duplicate_normalized_aliases_across_profiles()
    {
        var definitions = new[]
        {
            CreateDefinition("one", ["semi-novo"]),
            CreateDefinition("two", ["SEMI NÔVO"])
        };

        Assert.Throws<ArgumentException>(() =>
            new SemanticProfileCatalog(definitions));
    }

    [Fact]
    public void Constructor_rejects_duplicate_normalized_aliases_within_a_profile()
    {
        var definitions = new[]
        {
            CreateDefinition(
                "one",
                ["semi-novo", "SEMI NOVO"])
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            new SemanticProfileCatalog(definitions));

        Assert.Contains(
            "alias",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "duplic",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_rejects_contained_normalized_aliases_across_profiles()
    {
        var definitions = new[]
        {
            CreateDefinition("one", ["carro"]),
            CreateDefinition("two", ["meu carro"])
        };

        Assert.Throws<ArgumentException>(() =>
            new SemanticProfileCatalog(definitions));
    }

    [Fact]
    public void Constructor_rejects_overlapping_normalized_aliases_across_profiles()
    {
        var definitions = new[]
        {
            CreateDefinition("one", ["carro novo"]),
            CreateDefinition("two", ["novo barato"])
        };

        Assert.Throws<ArgumentException>(() =>
            new SemanticProfileCatalog(definitions));
    }

    [Fact]
    public void Constructor_allows_a_single_character_overlap_without_a_term_boundary()
    {
        var definitions = new[]
        {
            CreateDefinition("one", ["seminovo"]),
            CreateDefinition("two", ["oferta"])
        };

        var catalog = new SemanticProfileCatalog(definitions);

        Assert.Equal(2, catalog.Definitions.Count);
    }

    [Fact]
    public void Constructor_allows_overlapping_aliases_within_the_same_profile()
    {
        var catalog = new SemanticProfileCatalog(
        [
            CreateDefinition(
                "one",
                ["carro", "meu carro"])
        ]);
        var detector = new SemanticProfileDetector(catalog);

        var result = detector.Detect("meu carro branco");

        Assert.Equal(["one"], result.ProfileCodes);
        Assert.Equal("branco", result.SanitizedPrompt);
    }

    private static SemanticProfileDefinition CreateDefinition(
        string code,
        IReadOnlyList<string> aliases)
    {
        return new SemanticProfileDefinition(
            code,
            aliases,
            _ => new VehicleSearchCriteria());
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
