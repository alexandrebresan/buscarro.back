using Buscarro.Application.DTOs;
using Buscarro.Application.Handlers;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using Buscarro.Application.Services;

namespace Buscarro.Tests;

public sealed class AiSearchHandlerTests
{
    [Fact]
    public async Task Handle_sends_the_exact_sanitized_prompt_and_preserves_the_original_prompt()
    {
        var interpreter = new FakeAiPromptInterpreter(new VehicleSearchCriteria
        {
            Manufacturers = ["Honda"],
            Models = ["Civic"],
            Gears = ["automático"]
        });
        var handler = CreateHandler(
            interpreter,
            new SemanticProfileDetectionResult(
                ["semi_new", "beginner"],
                "Quero um Honda Civic automático"));

        var result = await handler.Handle(
            CreateQuery("Quero um Honda Civic seminovo para iniciante automático"),
            CancellationToken.None);

        Assert.Equal("Quero um Honda Civic automático", interpreter.ReceivedPrompt);
        Assert.Equal(
            "Quero um Honda Civic seminovo para iniciante automático",
            result.Prompt);
        Assert.Equal(
            "/api/vehicles?manufacturer=Honda&model=Civic&priceMax=65000&yearMin=2023&gear=autom%C3%A1tico&fuel=gasolina%20e%20%C3%A1lcool",
            result.SearchUrl);
        Assert.True(result.AiSearch);
    }

    [Fact]
    public async Task Handle_skips_the_interpreter_when_the_prompt_contains_only_profiles()
    {
        var interpreter = new FakeAiPromptInterpreter();
        var handler = CreateHandler(
            interpreter,
            new SemanticProfileDetectionResult(["semi_new"], "   "));

        var result = await handler.Handle(
            CreateQuery("seminovo"),
            CancellationToken.None);

        Assert.Equal(0, interpreter.CallCount);
        Assert.Equal("/api/vehicles?yearMin=2023", result.SearchUrl);
    }

    [Fact]
    public async Task Handle_with_real_detector_skips_ai_for_semi_new_with_only_fillers()
    {
        var interpreter = new FakeAiPromptInterpreter();
        var handler = CreateHandlerWithRealProfiles(interpreter);

        var result = await handler.Handle(
            CreateQuery("quero um carro seminovo"),
            CancellationToken.None);

        Assert.Equal(0, interpreter.CallCount);
        Assert.Equal("/api/vehicles?yearMin=2023", result.SearchUrl);
    }

    [Fact]
    public async Task Handle_with_real_detector_skips_ai_for_beginner_with_only_fillers()
    {
        var interpreter = new FakeAiPromptInterpreter();
        var handler = CreateHandlerWithRealProfiles(interpreter);

        var result = await handler.Handle(
            CreateQuery("busco um carro para iniciante"),
            CancellationToken.None);

        Assert.Equal(0, interpreter.CallCount);
        Assert.Equal(
            "/api/vehicles?priceMax=65000&yearMin=2014&gear=manual&fuel=gasolina%20e%20%C3%A1lcool",
            result.SearchUrl);
    }

    [Theory]
    [InlineData(
        "quero um carro seminovo automático",
        "quero um carro automático")]
    [InlineData(
        "quero um Honda seminovo",
        "quero um Honda")]
    [InlineData(
        "busco um carro seminovo em SP",
        "busco um carro em SP")]
    public async Task Handle_with_real_detector_calls_ai_when_an_objective_term_remains(
        string prompt,
        string expectedSanitizedPrompt)
    {
        var interpreter = new FakeAiPromptInterpreter();
        var handler = CreateHandlerWithRealProfiles(interpreter);

        await handler.Handle(CreateQuery(prompt), CancellationToken.None);

        Assert.Equal(1, interpreter.CallCount);
        Assert.Equal(expectedSanitizedPrompt, interpreter.ReceivedPrompt);
    }

    [Fact]
    public async Task Handle_respects_reverse_profile_order()
    {
        var handler = CreateHandler(
            new FakeAiPromptInterpreter(),
            new SemanticProfileDetectionResult(
                ["beginner", "semi_new"],
                "carro"));

        var result = await handler.Handle(
            CreateQuery("iniciante e seminovo"),
            CancellationToken.None);

        Assert.Contains("yearMin=2014", result.SearchUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("yearMin=2023", result.SearchUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_keeps_explicit_filters_over_profile_values()
    {
        var interpreter = new FakeAiPromptInterpreter(new VehicleSearchCriteria
        {
            Gears = ["automático"],
            PriceMax = 90_000,
            Fuels = ["elétrico"],
            YearMin = 2020
        });
        var handler = CreateHandler(
            interpreter,
            new SemanticProfileDetectionResult(["beginner"], "automático"));

        var result = await handler.Handle(
            CreateQuery("iniciante automático"),
            CancellationToken.None);

        Assert.Equal(
            "/api/vehicles?priceMax=90000&yearMin=2020&gear=autom%C3%A1tico&fuel=el%C3%A9trico",
            result.SearchUrl);
    }

    [Fact]
    public async Task Handle_allows_later_profiles_to_fill_free_fields()
    {
        var handler = CreateHandler(
            new FakeAiPromptInterpreter(),
            new SemanticProfileDetectionResult(
                ["semi_new", "beginner"],
                "carro"));

        var result = await handler.Handle(
            CreateQuery("seminovo para iniciante"),
            CancellationToken.None);

        Assert.Equal(
            "/api/vehicles?priceMax=65000&yearMin=2023&gear=manual&fuel=gasolina%20e%20%C3%A1lcool",
            result.SearchUrl);
    }

    [Fact]
    public async Task Handle_emits_every_explicit_alternative_as_repeated_public_parameter()
    {
        var interpreter=new FakeAiPromptInterpreter(new VehicleSearchCriteria { Manufacturers=["Fiat","Honda"],Colors=["branco","cinza"] });
        var result=await CreateHandler(interpreter,new SemanticProfileDetectionResult([],"Fiat ou Honda branco ou cinza"))
            .Handle(CreateQuery("Fiat ou Honda branco ou cinza"),CancellationToken.None);
        Assert.Equal("/api/vehicles?manufacturer=Fiat&manufacturer=Honda&color=branco&color=cinza",result.SearchUrl);
    }

    [Fact]
    public async Task Handle_rejects_whitespace_prompt_without_calling_interpreter()
    {
        var interpreter = new FakeAiPromptInterpreter();
        var detector = new ThrowingSemanticProfileDetector();
        var handler = CreateHandler(interpreter, detector);

        await Assert.ThrowsAsync<Buscarro.Application.Exceptions.ValidationException>(()=>handler.Handle(CreateQuery(" "), CancellationToken.None));
        Assert.Equal(0, interpreter.CallCount);
        Assert.Equal(0, detector.CallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_rejects_null_or_blank_prompt(string? prompt)
    {
        var interpreter=new FakeAiPromptInterpreter();var handler=CreateHandler(interpreter,new SemanticProfileDetectionResult([],""));
        var query=new AiSearchQuery(new AiSearchRequest { Prompt=prompt! });
        await Assert.ThrowsAsync<Buscarro.Application.Exceptions.ValidationException>(()=>handler.Handle(query,CancellationToken.None));
        Assert.Equal(0,interpreter.CallCount);
    }

    [Fact]
    public async Task Handle_rejects_prompt_over_2000_characters()
    {
        var interpreter=new FakeAiPromptInterpreter();var handler=CreateHandler(interpreter,new SemanticProfileDetectionResult([],""));
        await Assert.ThrowsAsync<Buscarro.Application.Exceptions.ValidationException>(()=>handler.Handle(CreateQuery(new string('x',2001)),CancellationToken.None));
        Assert.Equal(0,interpreter.CallCount);
    }

    [Fact]
    public async Task Handle_honors_cancellation_even_on_profile_only_path()
    {
        var interpreter=new FakeAiPromptInterpreter();var handler=CreateHandler(interpreter,new SemanticProfileDetectionResult(["semi_new"]," "));
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>handler.Handle(CreateQuery("seminovo"),cancellation.Token));
        Assert.Equal(0,interpreter.CallCount);
    }

    [Fact]
    public async Task Handle_propagates_interpreter_failure_when_explicit_text_remains()
    {
        var expected = new HttpRequestException("OpenAI indisponível");
        var interpreter = new FakeAiPromptInterpreter(exception: expected);
        var handler = CreateHandler(
            interpreter,
            new SemanticProfileDetectionResult(["beginner"], "Honda Civic"));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            handler.Handle(
                CreateQuery("Honda Civic para iniciante"),
                CancellationToken.None));

        Assert.Same(expected, exception);
        Assert.Equal(1, interpreter.CallCount);
    }

    [Fact]
    public async Task Handle_forwards_the_exact_cancellation_token_to_the_interpreter()
    {
        using var cancellation = new CancellationTokenSource();
        var interpreter = new FakeAiPromptInterpreter();
        var handler = CreateHandler(
            interpreter,
            new SemanticProfileDetectionResult([], "Honda"));

        await handler.Handle(CreateQuery("Honda"), cancellation.Token);

        Assert.Equal(cancellation.Token, interpreter.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Handle_loads_taxonomy_once_and_passes_same_snapshot()
    {
        var taxonomy = new VehicleSearchTaxonomy(
            Colors: ["azul lunar"],
            Gears: ["manual"],
            Fuels: ["elétrico"]);

        var taxonomyProvider =
            new FakeVehicleSearchTaxonomyProvider(
                taxonomy);

        var interpreter =
            new FakeAiPromptInterpreter(
                new VehicleSearchCriteria
                {
                    Colors = ["AZUL LUNAR"]
                });

        var handler = CreateHandler(
            interpreter,
            new SemanticProfileDetectionResult(
                [],
                "carro azul lunar"),
            taxonomyProvider);

        var result = await handler.Handle(
            CreateQuery("carro azul lunar"),
            CancellationToken.None);

        Assert.Equal(
            1,
            taxonomyProvider.CallCount);

        Assert.Same(
            taxonomy,
            interpreter.ReceivedTaxonomy);

        Assert.Equal(
            "/api/vehicles?color=azul%20lunar",
            result.SearchUrl);
    }

    private static AiSearchHandler CreateHandler(
        FakeAiPromptInterpreter interpreter,
        SemanticProfileDetectionResult detection,
        IVehicleSearchTaxonomyProvider? taxonomyProvider = null)
    {
        return CreateHandler(
            interpreter,
            new FakeSemanticProfileDetector(detection),
            taxonomyProvider);
    }

    private static AiSearchHandler CreateHandler(
        FakeAiPromptInterpreter interpreter,
        ISemanticProfileDetector detector,
        IVehicleSearchTaxonomyProvider? taxonomyProvider = null)
    {
        return new AiSearchHandler(
            interpreter,
            detector,
            new VehicleSearchCriteriaMerger(
                new SemanticProfileCatalog(),
                new FixedTimeProvider()),
            new VehicleSearchUrlBuilder(),
            new VehicleSearchPromptContentAnalyzer(),
            taxonomyProvider ??
                new FakeVehicleSearchTaxonomyProvider());
    }

    private static AiSearchHandler CreateHandlerWithRealProfiles(
        FakeAiPromptInterpreter interpreter)
    {
        var catalog = new SemanticProfileCatalog();

        return new AiSearchHandler(
            interpreter,
            new SemanticProfileDetector(catalog),
            new VehicleSearchCriteriaMerger(
                catalog,
                new FixedTimeProvider()),
            new VehicleSearchUrlBuilder(),
            new VehicleSearchPromptContentAnalyzer(),
            new FakeVehicleSearchTaxonomyProvider());
    }

    private static AiSearchQuery CreateQuery(string prompt)
    {
        return new AiSearchQuery(new AiSearchRequest { Prompt = prompt });
    }

    private sealed class FakeAiPromptInterpreter : IAiPromptInterpreter
    {
        private readonly VehicleSearchCriteria _criteria;
        private readonly Exception? _exception;

        public FakeAiPromptInterpreter(
            VehicleSearchCriteria? criteria = null,
            Exception? exception = null)
        {
            _criteria =
                criteria ?? new VehicleSearchCriteria();

            _exception = exception;
        }

        public int CallCount { get; private set; }

        public string? ReceivedPrompt { get; private set; }

        public VehicleSearchTaxonomy? ReceivedTaxonomy
        {
            get;
            private set;
        }

        public CancellationToken ReceivedCancellationToken
        {
            get;
            private set;
        }

        public Task<VehicleSearchCriteria> InterpretAsync(
            string sanitizedPrompt,
            VehicleSearchTaxonomy taxonomy,
            CancellationToken cancellationToken)
        {
            CallCount++;
            ReceivedPrompt = sanitizedPrompt;
            ReceivedTaxonomy = taxonomy;
            ReceivedCancellationToken = cancellationToken;

            return _exception is null
                ? Task.FromResult(_criteria)
                : Task.FromException<VehicleSearchCriteria>(
                    _exception);
        }
    }
    private sealed class FakeVehicleSearchTaxonomyProvider : IVehicleSearchTaxonomyProvider
    {
        private readonly VehicleSearchTaxonomy _taxonomy;

        public FakeVehicleSearchTaxonomyProvider(
            VehicleSearchTaxonomy? taxonomy = null)
        {
            _taxonomy = taxonomy ??
                TestVehicleSearchTaxonomy.Default;
        }

        public int CallCount { get; private set; }

        public Task<VehicleSearchTaxonomy> GetAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            return Task.FromResult(_taxonomy);
        }
    }
    private sealed class FakeSemanticProfileDetector(
        SemanticProfileDetectionResult result)
        : ISemanticProfileDetector
    {
        public SemanticProfileDetectionResult Detect(string prompt) => result;
    }

    private sealed class ThrowingSemanticProfileDetector : ISemanticProfileDetector
    {
        public int CallCount { get; private set; }
        public SemanticProfileDetectionResult Detect(string prompt)
        {
            CallCount++;
            throw new InvalidOperationException("Detector must not run for an invalid prompt.");
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
