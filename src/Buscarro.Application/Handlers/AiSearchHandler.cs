using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;
using Buscarro.Application.Interfaces;
using Buscarro.Application.Queries;
using Buscarro.Application.Services;
using MediatR;

namespace Buscarro.Application.Handlers;

public sealed class AiSearchHandler
    : IRequestHandler<AiSearchQuery, AiSearchResponse>
{
    public const int MaxPromptLength = 200;

    private readonly IAiPromptInterpreter _aiPromptInterpreter;
    private readonly ISemanticProfileDetector _profileDetector;
    private readonly VehicleSearchCriteriaMerger _criteriaMerger;
    private readonly VehicleSearchUrlBuilder _urlBuilder;
    private readonly VehicleSearchPromptContentAnalyzer
        _promptContentAnalyzer;
    private readonly IVehicleSearchTaxonomyProvider
        _taxonomyProvider;

    public AiSearchHandler(
        IAiPromptInterpreter aiPromptInterpreter,
        ISemanticProfileDetector profileDetector,
        VehicleSearchCriteriaMerger criteriaMerger,
        VehicleSearchUrlBuilder urlBuilder,
        VehicleSearchPromptContentAnalyzer promptContentAnalyzer,
        IVehicleSearchTaxonomyProvider taxonomyProvider)
    {
        _aiPromptInterpreter = aiPromptInterpreter ??
            throw new ArgumentNullException(
                nameof(aiPromptInterpreter));

        _profileDetector = profileDetector ??
            throw new ArgumentNullException(
                nameof(profileDetector));

        _criteriaMerger = criteriaMerger ??
            throw new ArgumentNullException(
                nameof(criteriaMerger));

        _urlBuilder = urlBuilder ??
            throw new ArgumentNullException(
                nameof(urlBuilder));

        _promptContentAnalyzer = promptContentAnalyzer ??
            throw new ArgumentNullException(
                nameof(promptContentAnalyzer));

        _taxonomyProvider = taxonomyProvider ??
            throw new ArgumentNullException(
                nameof(taxonomyProvider));
    }

    public async Task<AiSearchResponse> Handle(
        AiSearchQuery request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Request);

        var originalPrompt =
            request.Request.Prompt;

        if (string.IsNullOrWhiteSpace(originalPrompt))
        {
            throw new ValidationException(
                "Prompt e obrigatorio.");
        }

        if (originalPrompt.Length > MaxPromptLength)
        {
            throw new ValidationException(
                $"Prompt nao pode exceder " +
                $"{MaxPromptLength} caracteres.");
        }

        var detection =
            _profileDetector.Detect(originalPrompt);

        var taxonomy =
            await _taxonomyProvider.GetAsync(
                cancellationToken);

        var explicitCriteria =
            detection.ProfileCodes.Count > 0 &&
            !_promptContentAnalyzer.HasMeaningfulTerms(
                detection.SanitizedPrompt)
                ? new VehicleSearchCriteria()
                : await _aiPromptInterpreter.InterpretAsync(
                    detection.SanitizedPrompt,
                    taxonomy,
                    cancellationToken);

        var criteria = _criteriaMerger.Merge(
            explicitCriteria,
            detection.ProfileCodes,
            taxonomy);

        cancellationToken.ThrowIfCancellationRequested();

        return new AiSearchResponse
        {
            Prompt = originalPrompt,

            SearchUrl = _urlBuilder.Build(
                criteria,
                taxonomy),

            AiSearch = true
        };
    }
}