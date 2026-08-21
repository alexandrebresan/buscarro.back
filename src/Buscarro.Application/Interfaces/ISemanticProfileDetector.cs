using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface ISemanticProfileDetector
{
    SemanticProfileDetectionResult Detect(string prompt);
}
