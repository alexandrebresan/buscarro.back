using System.Net;
using System.Text;
using System.Text.Json;
using Buscarro.Application.DTOs;
using Buscarro.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Buscarro.Tests;

public sealed class AiPromptInterpreterTests
{
    [Fact]
    public async Task InterpretAsync_returns_the_structured_criteria()
    {
        var handler = new RecordingHttpMessageHandler(CreateOpenAiResponse(ValidCriteriaJson));
        var interpreter = CreateInterpreter(handler);

        var result = await interpreter.InterpretAsync(
            "Fiat Uno branco",
            TestVehicleSearchTaxonomy.Default,
            CancellationToken.None);

        Assert.Equal(["Fiat"], result.Manufacturers);
        Assert.Equal(["Uno"], result.Models);
        Assert.Equal(["branco"], result.Colors);
        Assert.Equal(["manual"], result.Gears);
        Assert.Equal(["gasolina e álcool"], result.Fuels);
    }

    [Fact]
    public async Task InterpretAsync_sends_the_exact_sanitized_prompt_as_user_content()
    {
        var handler = new RecordingHttpMessageHandler(CreateOpenAiResponse(ValidCriteriaJson));
        var interpreter = CreateInterpreter(handler);

        await interpreter.InterpretAsync(
            "Quero um Honda Civic automático",
            TestVehicleSearchTaxonomy.Default,
            CancellationToken.None);

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal(
            "Quero um Honda Civic automático",
            messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task InterpretAsync_sends_the_exact_strict_plural_schema()
    {
        var handler = new RecordingHttpMessageHandler(CreateOpenAiResponse(ValidCriteriaJson));
        var interpreter = CreateInterpreter(handler);

        await interpreter.InterpretAsync(
            "carro branco manual flex",
            TestVehicleSearchTaxonomy.Default,
            CancellationToken.None);

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var properties = body.RootElement
            .GetProperty("response_format")
            .GetProperty("json_schema")
            .GetProperty("schema")
            .GetProperty("properties");

        var schema=body.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("schema");
        Assert.True(body.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[]{"providers","states","cities","manufacturers","models","versions","colors","gears","fuels","priceMin","priceMax","yearMin","yearMax"},
            schema.GetProperty("required").EnumerateArray().Select(value=>value.GetString()).ToArray());
        Assert.Equal(schema.GetProperty("required").EnumerateArray().Select(v=>v.GetString()).OrderBy(v=>v).ToArray(),
            properties.EnumerateObject().Select(p=>p.Name).OrderBy(v=>v).ToArray());
        foreach (var propertyName in new[]{"providers","states","cities","manufacturers","models","versions","colors","gears","fuels"})
        {
            var property=properties.GetProperty(propertyName);
            Assert.Equal("array",property.GetProperty("type").GetString());
            Assert.Equal("string",property.GetProperty("items").GetProperty("type").GetString());
            Assert.False(property.TryGetProperty("maxItems",out _));
        }
        Assert.Equal(["number","null"],properties.GetProperty("priceMin").GetProperty("type").EnumerateArray().Select(v=>v.GetString()!).ToArray());
        Assert.Equal(["integer","null"],properties.GetProperty("yearMax").GetProperty("type").EnumerateArray().Select(v=>v.GetString()!).ToArray());
        Assert.DoesNotContain("maxItems",handler.RequestBody!,StringComparison.Ordinal);
        AssertCanonicalEnum(
            properties,
            "colors",
            TestVehicleSearchTaxonomy.Default.Colors);

        AssertCanonicalEnum(
            properties,
            "gears",
            TestVehicleSearchTaxonomy.Default.Gears);

        AssertCanonicalEnum(
            properties,
            "fuels",
            TestVehicleSearchTaxonomy.Default.Fuels);
    }

    [Fact]
    public async Task InterpretAsync_payload_contains_no_semantic_profile_aliases_rules_or_codes()
    {
        var handler = new RecordingHttpMessageHandler(CreateOpenAiResponse(ValidCriteriaJson));
        var interpreter = CreateInterpreter(handler);

        await interpreter.InterpretAsync(
            "Honda Civic",
            TestVehicleSearchTaxonomy.Default,
            CancellationToken.None);

        using var body = JsonDocument.Parse(handler.RequestBody!);
        var systemPrompt = body.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetString()!;

        Assert.Contains(
            "extraia apenas filtros objetivos explicitamente informados",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "não converta intenções subjetivas em filtros",
            systemPrompt,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("alternativas OR",systemPrompt,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("máximo 20",systemPrompt,StringComparison.OrdinalIgnoreCase);

        foreach (var forbidden in new[]
                 {
                     "semi_new", "beginner", "seminovo", "semi-novo",
                     "iniciante", "primeiro carro", "65000", "2014"
                 })
        {
            Assert.DoesNotContain(forbidden, handler.RequestBody!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task InterpretAsync_throws_when_openai_returns_empty_content()
    {
        var handler = new RecordingHttpMessageHandler(CreateOpenAiResponse(" "));
        var interpreter = CreateInterpreter(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            interpreter.InterpretAsync(
                "Honda Civic",
                TestVehicleSearchTaxonomy.Default,
                CancellationToken.None));

        Assert.Equal("A OpenAI não retornou filtros de busca.", exception.Message);
    }

    [Fact]
    public async Task InterpretAsync_throws_when_openai_returns_invalid_json()
    {
        var handler = new RecordingHttpMessageHandler(CreateOpenAiResponse("{invalid"));
        var interpreter = CreateInterpreter(handler);

        await Assert.ThrowsAsync<JsonException>(() =>
            interpreter.InterpretAsync(
                "Honda Civic",
                TestVehicleSearchTaxonomy.Default,
                CancellationToken.None));
    }

    private static AiPromptInterpreter CreateInterpreter(
        RecordingHttpMessageHandler handler)
    {
        return new AiPromptInterpreter(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.openai.com/")
            },
            Options.Create(new OpenAiOptions
            {
                Model = "gpt-4o-mini-test"
            }),
            new OpenAiVehicleSearchCriteriaMapper(new Application.Services.VehicleSearchCriteriaCanonicalizer()));
    }

    private static void AssertCanonicalEnum(
        JsonElement properties,
        string propertyName,
        IReadOnlyList<string> expected)
    {
        var actual = properties
            .GetProperty(propertyName)
            .GetProperty("items").GetProperty("enum")
            .EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()!)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    private static HttpResponseMessage CreateOpenAiResponse(string content)
    {
        var payload = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content } }
            }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private static readonly string ValidCriteriaJson = """
        {
          "providers": [],
          "states": [],
          "cities": [],
          "manufacturers": ["Fiat"],
          "models": ["Uno"],
          "versions": [],
          "colors": ["branco"],
          "gears": ["manual"],
          "fuels": ["gasolina e álcool"],
          "priceMin": null,
          "priceMax": null,
          "yearMin": null,
          "yearMax": null
        }
        """;

    private sealed class RecordingHttpMessageHandler(
        HttpResponseMessage response)
        : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return response;
        }
    }

    [Fact]
    public async Task InterpretAsync_builds_schema_with_received_taxonomy()
    {
        const string responseJson = """
            {
            "providers": [],
            "states": [],
            "cities": [],
            "manufacturers": [],
            "models": [],
            "versions": [],
            "colors": ["azul lunar"],
            "gears": ["manual"],
            "fuels": ["elétrico"],
            "priceMin": null,
            "priceMax": null,
            "yearMin": null,
            "yearMax": null
            }
            """;

        var taxonomy = new VehicleSearchTaxonomy(
            Colors: ["azul lunar"],
            Gears: ["manual"],
            Fuels: ["elétrico"]);

        var handler = new RecordingHttpMessageHandler(
            CreateOpenAiResponse(responseJson));

        var interpreter = CreateInterpreter(handler);

        var result = await interpreter.InterpretAsync(
            "carro azul lunar",
            taxonomy,
            CancellationToken.None);

        using var body = JsonDocument.Parse(
            handler.RequestBody!);

        var properties = body.RootElement
            .GetProperty("response_format")
            .GetProperty("json_schema")
            .GetProperty("schema")
            .GetProperty("properties");

        AssertCanonicalEnum(
            properties,
            "colors",
            taxonomy.Colors);

        AssertCanonicalEnum(
            properties,
            "gears",
            taxonomy.Gears);

        AssertCanonicalEnum(
            properties,
            "fuels",
            taxonomy.Fuels);

        Assert.Equal(
            ["azul lunar"],
            result.Colors);
    }
}
