using System.Net.Http.Headers;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Services;
using Buscarro.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Buscarro.Api.Configurations;

public static class AiSearchServiceCollectionExtensions
{
    public static IServiceCollection AddAiSearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IVehicleSearchTaxonomyProvider, StaticVehicleSearchTaxonomyProvider>();
        services.AddSingleton<SemanticProfileCatalog>();
        services.AddSingleton<
            ISemanticProfileDetector,
            SemanticProfileDetector>();
        services.AddSingleton<VehicleSearchPromptContentAnalyzer>();
        services.AddSingleton<VehicleSearchCriteriaCanonicalizer>();
        services.AddSingleton<VehicleSearchCriteriaMerger>();
        services.AddSingleton<VehicleSearchUrlBuilder>();
        services.AddSingleton<OpenAiVehicleSearchCriteriaMapper>();
        services.Configure<OpenAiOptions>(
            configuration.GetSection(OpenAiOptions.SectionName));
        services.AddHttpClient<
            IAiPromptInterpreter,
            AiPromptInterpreter>((serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<OpenAiOptions>>()
                    .Value;
                client.BaseAddress = new Uri(options.BaseUrl);

                var apiKey =
                    Environment.GetEnvironmentVariable(
                        "OPENAI_API_KEY") ??
                    options.ApiKey;
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue(
                            "Bearer",
                            apiKey);
                }
            });

        return services;
    }
}
