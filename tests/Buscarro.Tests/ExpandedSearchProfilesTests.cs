using System.Text.Json;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Handlers;
using Buscarro.Application.Queries;
using Buscarro.Application.Services;
using Buscarro.Infrastructure.Repositories;
using Buscarro.Infrastructure.Services;

namespace Buscarro.Tests;

public sealed class ExpandedSearchProfilesTests
{
    [Theory]
    [InlineData("Quero um SUV", "suv")]
    [InlineData("Quero um sedan", "sedan")]
    [InlineData("Quero um hatch", "hatch")]
    [InlineData("um carro pra familia", "family")]
    [InlineData("Quero um carro economico", "economic")]
    [InlineData("carro divertido", "fun")]
    [InlineData("Carro grande", "large")]
    public async Task Model_profiles_generate_compact_urls_without_calling_ai(string prompt, string code)
    {
        var interpreter = new Interpreter();
        var response = await Handler(interpreter).Handle(new(new() { Prompt = prompt }), CancellationToken.None);
        Assert.Equal($"/api/vehicles?profile={code}", response.SearchUrl);
        Assert.Equal(0, interpreter.Calls);
    }

    [Theory]
    [InlineData("Quero um carro pouco rodado", "mileageMax=30000")]
    [InlineData("Quero um carro antigo", "yearMax=2006")]
    [InlineData("Carro moderno", "yearMin=2021")]
    public async Task Range_profiles_use_fixed_km_and_relative_years(string prompt, string parameter)
    {
        var interpreter = new Interpreter();
        var response = await Handler(interpreter).Handle(new(new() { Prompt = prompt }), CancellationToken.None);
        Assert.Contains(parameter, response.SearchUrl);
        Assert.Equal(0, interpreter.Calls);
    }

    [Theory]
    [InlineData("Carro com menos de 50 mil km", "{\"mileageMax\":49999}", "mileageMax=49999")]
    [InlineData("Quero os carros mais baratos", "{\"order\":\"price\"}", "order=price")]
    [InlineData("quero um carro 2/4 portas", "{\"doors\":[2,4]}", "doors=2&doors=4")]
    public async Task Objective_filters_from_interpreter_reach_url(string prompt, string criteria, string parameter)
    {
        var interpreter = new Interpreter(criteria);
        var response = await Handler(interpreter).Handle(new(new() { Prompt = prompt }), CancellationToken.None);
        Assert.Contains(parameter, response.SearchUrl);
        Assert.Equal(prompt, interpreter.Prompt);
    }

    [Fact]
    public async Task Mixed_prompt_sends_only_remainder_and_explicit_km_overrides_profile()
    {
        var interpreter = new Interpreter("{\"mileageMax\":60000,\"models\":[\"Civic\"]}");
        var response = await Handler(interpreter).Handle(new(new() { Prompt = "Civic econômico pouco rodado até 60 mil km" }), CancellationToken.None);
        Assert.Equal("Civic até 60 mil km", interpreter.Prompt);
        Assert.Contains("profile=economic", response.SearchUrl);
        Assert.Contains("model=Civic", response.SearchUrl);
        Assert.Contains("mileageMax=60000", response.SearchUrl);
    }

    [Theory]
    [InlineData("não quero um SUV")]
    [InlineData("sem hatch")]
    [InlineData("não precisa ser econômico")]
    [InlineData("não quero um carro econômico")]
    [InlineData("não quero um carro antigo")]
    public void Negated_profiles_do_not_become_positive_filters(string prompt)
        => Assert.Empty(new SemanticProfileDetector(new()).Detect(prompt).ProfileCodes);

    [Fact]
    public void Reform_is_not_a_profile()
        => Assert.Empty(new SemanticProfileDetector(new()).Detect("Carro para reformar").ProfileCodes);

    [Theory]
    [InlineData("Carro em Campo Grande")]
    [InlineData("Carro em Praia Grande")]
    [InlineData("Carros no Rio Grande do Sul")]
    [InlineData("Campo Grande")]
    [InlineData("Carros na Grande São Paulo")]
    [InlineData("Quero os carros mais antigos")]
    [InlineData("Quero os carros mais modernos")]
    public void Objective_place_names_and_sorting_are_preserved_for_ai(string prompt)
    {
        var detection = new SemanticProfileDetector(new()).Detect(prompt);
        Assert.Empty(detection.ProfileCodes);
        Assert.Equal(prompt, detection.SanitizedPrompt);
    }

    [Fact]
    public void Size_before_location_is_preserved_while_city_name_is_not_consumed()
    {
        var detection = new SemanticProfileDetector(new()).Detect("Carro grande em Campo Grande");
        Assert.Equal(["large"], detection.ProfileCodes);
        Assert.Equal("Carro em Campo Grande", detection.SanitizedPrompt);
    }

    [Theory]
    [InlineData("SUVs econômicos", "suv", "economic")]
    [InlineData("sedãs familiares", "sedan", "family")]
    [InlineData("hatches divertidos", "hatch", "fun")]
    [InlineData("não quero um SUV, mas quero um hatch", "hatch", null)]
    [InlineData("não bebe muito", "economic", null)]
    public void Detect_handles_plural_accents_combinations_and_negation_scope(string prompt, string first, string? second)
    {
        var codes = new SemanticProfileDetector(new()).Detect(prompt).ProfileCodes;
        Assert.Contains(first, codes);
        Assert.Equal(second is null ? 1 : 2, codes.Count);
        if (second is not null) Assert.Contains(second, codes);
    }

    [Theory]
    [InlineData("old", "{\"yearMin\":2010}")]
    [InlineData("modern", "{\"yearMax\":2000}")]
    [InlineData("low_mileage", "{\"mileageMin\":60000}")]
    public void Explicit_dimension_replaces_both_sides_of_profile_default(string code, string json)
    {
        var source = JsonSerializer.Deserialize<VehicleSearchCriteria>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var result = new VehicleSearchCriteriaMerger(new(), new Clock()).Merge(source, [code], TestVehicleSearchTaxonomy.Default);
        Assert.Equal(source.YearMin, result.YearMin);
        Assert.Equal(source.YearMax, result.YearMax);
        Assert.Equal(source.MileageMin, result.MileageMin);
        Assert.Equal(source.MileageMax, result.MileageMax);
    }

    [Fact]
    public void Model_groups_match_source_memberships_without_scores_or_fuzzy_title_matching()
    {
        var economic = SemanticVehicleModelCatalog.GetRequired("economic");
        Assert.Equal(106, economic.Count);
        Assert.Contains(economic, model => model.Brand == "chevrolet" && model.Model == "onix");
        Assert.DoesNotContain(economic, model => model.Brand == "honda" && model.Model == "civic");
        var large = SemanticVehicleModelCatalog.GetRequired("large");
        Assert.Equal(74, large.Count);
        Assert.Contains(large, model => model.Brand == "volvo" && model.Model == "s90");
        Assert.Contains(SemanticVehicleModelCatalog.GetRequired("suv"), model => model.Brand == "jeep" && model.Model == "compass");
        foreach (var code in new[] { "suv", "sedan", "hatch", "economic", "family", "fun", "large" })
        {
            var models = SemanticVehicleModelCatalog.GetRequired(code);
            Assert.Equal(models.Count, models.Select(model => (model.Brand, model.Model)).Distinct().Count());
            Assert.All(models, model => Assert.Contains(code, model.Profiles));
        }
    }

    [Fact]
    public void Group_and_door_order_does_not_fragment_cache()
    {
        var first = Normalize("{\"profile\":[\"suv\",\"economic\",\"SUV\"],\"doors\":[4,2,4]}");
        var second = Normalize("{\"profile\":[\"economic\",\"suv\"],\"doors\":[2,4]}");
        Assert.Equal(CatalogQueryCache.BuildResultKey(first), CatalogQueryCache.BuildResultKey(second));
    }

    [Fact]
    public void Sql_applies_paired_models_and_objective_fields_as_parameters()
    {
        var criteria = Normalize("{\"profile\":[\"suv\",\"economic\"],\"mileageMax\":30000,\"doors\":[2,4]}");
        var predicate = new VehicleCatalogSqlPredicateBuilder().Build(criteria, [1], ["Carzin"]);
        Assert.Contains("v.mileage <= @mileage_max", predicate.WhereSql);
        Assert.Contains("v.doors = ANY(@doors)", predicate.WhereSql);
        Assert.Contains("v.brand_normalized", predicate.WhereSql);
        Assert.Contains("v.model_normalized", predicate.WhereSql);
        Assert.DoesNotContain("search_vector", predicate.WhereSql);
        Assert.Equal(2, predicate.Parameters.Count(p => p.ParameterName.StartsWith("profile_brands_")));
        Assert.DoesNotContain("Jeep", predicate.WhereSql);
    }

    [Fact]
    public void Cache_keys_distinguish_all_new_filters()
    {
        var empty = Normalize("{}");
        foreach (var json in new[] { "{\"profile\":[\"suv\"]}", "{\"doors\":[4]}", "{\"mileageMin\":100}", "{\"mileageMax\":30000}" })
        {
            var criteria = Normalize(json);
            Assert.NotEqual(CatalogQueryCache.BuildResultKey(empty), CatalogQueryCache.BuildResultKey(criteria));
            Assert.NotEqual(CatalogQueryCache.BuildCountKey(1, empty), CatalogQueryCache.BuildCountKey(1, criteria));
            Assert.NotEqual(CatalogQueryCache.BuildPageKey(1, empty), CatalogQueryCache.BuildPageKey(1, criteria));
        }
    }

    [Theory]
    [InlineData("{\"profile\":[\"unknown\"]}")]
    [InlineData("{\"doors\":[0]}")]
    [InlineData("{\"mileageMin\":-1}")]
    [InlineData("{\"mileageMin\":50,\"mileageMax\":10}")]
    public void Invalid_new_filters_are_rejected(string json)
        => Assert.Throws<Buscarro.Application.Exceptions.ValidationException>(() => Normalize(json));

    private static NormalizedVehicleCatalogSearchCriteria Normalize(string json)
        => new VehicleCatalogSearchCriteriaNormalizer().Normalize(JsonSerializer.Deserialize<VehicleCatalogSearchRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!);

    private static AiSearchHandler Handler(Interpreter interpreter)
    {
        var catalog = new SemanticProfileCatalog();
        var clock = new Clock();
        return new(interpreter, new SemanticProfileDetector(catalog), new(catalog, clock), new(), new(), new StaticVehicleSearchTaxonomyProvider());
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class Interpreter(string json = "{}") : IAiPromptInterpreter
    {
        public int Calls { get; private set; }
        public string? Prompt { get; private set; }
        public Task<VehicleSearchCriteria> InterpretAsync(string sanitizedPrompt, VehicleSearchTaxonomy taxonomy, CancellationToken cancellationToken)
        {
            Calls++;
            Prompt = sanitizedPrompt;
            return Task.FromResult(JsonSerializer.Deserialize<VehicleSearchCriteria>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
        }
    }
}
