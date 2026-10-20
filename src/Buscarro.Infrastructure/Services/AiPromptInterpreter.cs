using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Infrastructure.Contracts;
using Microsoft.Extensions.Options;

namespace Buscarro.Infrastructure.Services;

public sealed class AiPromptInterpreter
    : IAiPromptInterpreter
{
    private static readonly string SystemPrompt = """
        Você converte pedidos de busca de veículos em filtros estruturados.

        Retorne somente os campos definidos no schema recebido.

        Extraia apenas filtros objetivos explicitamente informados.

        Não converta intenções subjetivas em filtros; perfis são tratados fora do modelo.

        Os filtros categóricos são arrays; doors é um array de inteiros.
        Extraia mileageMin e mileageMax em quilômetros. Ausentes são null.
        "menos de 50 mil km" => mileageMax: 49999; "até 50 mil km" => mileageMax: 50000.
        "2/4 portas" ou "2 ou 4 portas" => doors: [2, 4]. Ausente => [].
        Para order use price (mais baratos), price-inverse (mais caros),
        year (mais antigos), year-inverse (mais novos ou mais modernos), mileage (menos rodados)
        ou mileage-inverse (mais rodados). Ausente => null.
        Não transforme um critério negado em filtro positivo.

        Todos os dez filtros categóricos textuais são arrays.

        Use [] quando ausentes e no máximo 20 valores.

        Valores explícitos da mesma categoria são alternativas OR e todos devem ser preservados.

        Providers, countries, states, cities e manufacturers são nomes, nunca IDs.

        Quando state ou city identificar inequivocamente um país, inclua também country para manter a hierarquia de localização.

        Para localidades brasileiras, utilize Brasil como country.

        Para localidades dos Estados Unidos, utilize Estados Unidos como country.

        Models e versions também são nomes quando contêm apenas dígitos
        (ex.: 208, 500, 2008, 911);
        nunca os trate como IDs de provider.

        Colors, gears e fuels usam somente valores canônicos do schema quando inequívocos.

        Não invente valores.

        Use null para ranges e order ausentes.

        Exemplo:
        Fiat ou Honda branco ou cinza
        =>
        manufacturers: [Fiat, Honda],
        colors: [branco, cinza].

        Exemplo:
        Corolla em São Paulo
        =>
        country: [Brasil],
        state: [SP ou São Paulo, conforme informado],
        model: [Corolla].

        Exemplo:
        Civic na Flórida
        =>
        country: [Estados Unidos],
        state: [Florida],
        model: [Civic].
        """;

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive =
                true
        };

    private static readonly object NullableIntegerSchema =
        new
        {
            type = new[]
            {
                "integer",
                "null"
            }
        };

    private static readonly object NullableNumberSchema =
        new
        {
            type = new[]
            {
                "number",
                "null"
            }
        };

    private static readonly object StringArraySchema =
        new
        {
            type =
                "array",

            items =
                new
                {
                    type =
                        "string"
                }
        };

    private readonly HttpClient _httpClient;

    private readonly OpenAiOptions _options;

    private readonly OpenAiVehicleSearchCriteriaMapper _mapper;

    public AiPromptInterpreter(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        OpenAiVehicleSearchCriteriaMapper mapper)
    {
        _httpClient =
            httpClient ??
            throw new ArgumentNullException(
                nameof(httpClient));

        ArgumentNullException.ThrowIfNull(
            options);

        _options =
            options.Value;

        _mapper =
            mapper ??
            throw new ArgumentNullException(
                nameof(mapper));
    }

    public async Task<VehicleSearchCriteria> InterpretAsync(
        string sanitizedPrompt,
        VehicleSearchTaxonomy taxonomy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            taxonomy);

        var responseSchema =
            CreateResponseSchema(
                taxonomy);

        var payload =
            new
            {
                model =
                    _options.Model,

                messages =
                    new[]
                    {
                        new
                        {
                            role =
                                "system",

                            content =
                                SystemPrompt
                        },

                        new
                        {
                            role =
                                "user",

                            content =
                                sanitizedPrompt
                        }
                    },

                response_format =
                    new
                    {
                        type =
                            "json_schema",

                        json_schema =
                            new
                            {
                                name =
                                    "vehicle_search_criteria",

                                strict =
                                    true,

                                schema =
                                    responseSchema
                            }
                    }
            };

        using var response =
            await _httpClient.PostAsJsonAsync(
                "v1/chat/completions",
                payload,
                SerializerOptions,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content
                .ReadAsStreamAsync(
                    cancellationToken);

        var completion =
            await JsonSerializer
                .DeserializeAsync<OpenAiChatCompletion>(
                    stream,
                    SerializerOptions,
                    cancellationToken);

        var content =
            completion?
                .Choices
                .FirstOrDefault()?
                .Message
                .Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                "A OpenAI não retornou filtros de busca.");
        }

        var contract =
            JsonSerializer
                .Deserialize<OpenAiVehicleSearchCriteriaContract>(
                    content,
                    SerializerOptions)
            ??
            throw new InvalidOperationException(
                "A OpenAI retornou filtros de busca inválidos.");

        return _mapper.Map(
            contract,
            taxonomy);
    }

    private static object CreateResponseSchema(
        VehicleSearchTaxonomy taxonomy)
    {
        return new
        {
            type =
                "object",

            properties =
                new
                {
                    providers =
                        StringArraySchema,

                    countries =
                        StringArraySchema,

                    states =
                        StringArraySchema,

                    cities =
                        StringArraySchema,

                    manufacturers =
                        StringArraySchema,

                    models =
                        StringArraySchema,

                    versions =
                        StringArraySchema,

                    colors =
                        CanonicalArray(
                            taxonomy.Colors),

                    gears =
                        CanonicalArray(
                            taxonomy.Gears),

                    fuels =
                        CanonicalArray(
                            taxonomy.Fuels),

                    priceMin =
                        NullableNumberSchema,

                    priceMax =
                        NullableNumberSchema,

                    yearMin =
                        NullableIntegerSchema,

                    yearMax =
                        NullableIntegerSchema,
                    mileageMin = NullableIntegerSchema,
                    mileageMax = NullableIntegerSchema,
                    doors = new { type = "array", items = new { type = "integer", minimum = 1, maximum = 6 } },
                    order = new
                    {
                        type = new[] { "string", "null" },
                        @enum = new string?[] { null, "price", "price-inverse", "year", "year-inverse", "mileage", "mileage-inverse" }
                    }
                },

            required =
                new[]
                {
                    "providers",
                    "countries",
                    "states",
                    "cities",
                    "manufacturers",
                    "models",
                    "versions",
                    "colors",
                    "gears",
                    "fuels",
                    "priceMin",
                    "priceMax",
                    "yearMin",
                    "yearMax",
                    "mileageMin",
                    "mileageMax",
                    "doors",
                    "order"
                },

            additionalProperties =
                false
        };
    }

    private static object CanonicalArray(
        IReadOnlyList<string> values)
    {
        return new
        {
            type =
                "array",

            items =
                new
                {
                    type =
                        "string",

                    @enum =
                        values
                }
        };
    }

    private sealed class OpenAiChatCompletion
    {
        [JsonPropertyName("choices")]
        public IReadOnlyList<OpenAiChoice> Choices
        {
            get;
            init;
        } = [];
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiMessage Message
        {
            get;
            init;
        } = new();
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("content")]
        public string Content
        {
            get;
            init;
        } = string.Empty;
    }
}

public sealed class OpenAiOptions
{
    public const string SectionName =
        "OpenAI";

    public string Model
    {
        get;
        set;
    } = "gpt-4o-mini";

    public string BaseUrl
    {
        get;
        set;
    } = "https://api.openai.com/";

    public string ApiKey
    {
        get;
        set;
    } = string.Empty;
}
