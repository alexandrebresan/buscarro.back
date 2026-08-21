namespace Buscarro.Application.DTOs;

public sealed record SemanticProfileDetectionResult(
    IReadOnlyList<string> ProfileCodes,
    string SanitizedPrompt);
