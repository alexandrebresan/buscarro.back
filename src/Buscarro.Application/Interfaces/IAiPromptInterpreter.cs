using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface IAiPromptInterpreter
{
    Task<VehicleSearchCriteria> InterpretAsync(
        string sanitizedPrompt,
        VehicleSearchTaxonomy taxonomy,
        CancellationToken cancellationToken);
}