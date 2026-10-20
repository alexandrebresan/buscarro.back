using System.Collections.Frozen;
using System.Text.Json;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;

namespace Buscarro.Application.Services;

/// <summary>
/// Fixed model groups derived from BUSCARRO_BASE_COMPLETA v1.0 (2026-08-24).
/// Model-level guidance only: it does not establish facts about an individual version.
/// </summary>
public static class SemanticVehicleModelCatalog
{
    private static readonly FrozenDictionary<string, IReadOnlyList<SemanticVehicleModel>> Groups = Load();

    public static IReadOnlyList<SemanticVehicleModel> GetRequired(string code)
        => Groups.TryGetValue(code, out var models)
            ? models
            : throw new ValidationException($"profile '{code}' invalido.");

    public static List<string> Canonicalize(IEnumerable<string> codes)
    {
        var values = codes.ToArray();
        if (values.Length > 20)
            throw new ValidationException("profile excede 20 valores.");
        var result = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ValidationException("profile nao pode ser vazio.");
            var code = value.Trim().ToLowerInvariant();
            GetRequired(code);
            if (!result.Contains(code)) result.Add(code);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static FrozenDictionary<string, IReadOnlyList<SemanticVehicleModel>> Load()
    {
        using var stream = typeof(SemanticVehicleModelCatalog).Assembly
            .GetManifestResourceStream("Buscarro.Application.Resources.SemanticVehicleModels.json")
            ?? throw new InvalidOperationException("Semantic model resource is missing.");
        var models = JsonSerializer.Deserialize<SemanticVehicleModel[]>(stream)
            ?? throw new InvalidOperationException("Semantic model resource is invalid.");
        return models.SelectMany(model => model.Profiles.Select(code => (Code: code, Model: model)))
            .GroupBy(entry => entry.Code, StringComparer.Ordinal)
            .ToFrozenDictionary(group => group.Key,
                group => (IReadOnlyList<SemanticVehicleModel>)Array.AsReadOnly(group.Select(entry => entry.Model with
                {
                    Brand = VehicleCatalogSearchCriteriaNormalizer.NormalizeText(entry.Model.Brand),
                    Model = VehicleCatalogSearchCriteriaNormalizer.NormalizeText(entry.Model.Model),
                    Profiles = Array.AsReadOnly(entry.Model.Profiles.ToArray())
                }).ToArray()), StringComparer.Ordinal);
    }
}
