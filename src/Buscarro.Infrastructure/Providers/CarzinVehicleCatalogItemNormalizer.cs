using System.Collections.ObjectModel;
using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;

namespace Buscarro.Infrastructure.Providers;

public sealed class CarzinVehicleCatalogItemNormalizer(ICarzinVehicleCatalogTaxonomyReader reader)
    : IVehicleCatalogItemNormalizer
{
    private IReadOnlyDictionary<string, string>? _manufacturers;
    private IReadOnlyDictionary<(string Manufacturer, string Model), string>? _models;
    private IReadOnlyDictionary<(string Manufacturer, string Model, string Version), string>? _versions;

    public string Provider => "Carzin";

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        var snapshot = await reader.ReadSnapshotAsync(cancellationToken);
        ValidateCompleteness(snapshot);
        try
        {
            var manufacturerIds = snapshot.Manufacturers.Select(value => value.Id).ToHashSet();
            var modelIds = snapshot.Models.Select(value => value.Id).ToHashSet();
            if (snapshot.Models.Any(value => !manufacturerIds.Contains(value.ManufacturerId)) ||
                snapshot.Versions.Any(value => !modelIds.Contains(value.ModelId)))
                throw new InvalidOperationException("Invalid taxonomy hierarchy.");
            _manufacturers = ReadOnly(snapshot.Manufacturers, value => Key(value.Code), value => Name(value.Name));
            _models = ReadOnly(snapshot.Models, value => (Key(value.ManufacturerCode), Key(value.Code)), value => Name(value.Name));
            _versions = ReadOnly(snapshot.Versions,
                value => (Key(value.ManufacturerCode), Key(value.ModelCode), Key(value.Code)), value => Name(value.Name));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new InvalidOperationException("carzin_taxonomy_corrupt");
        }
    }

    public CatalogItemNormalizationResult Normalize(CatalogSourceVehicle source)
    {
        if (_manufacturers is null || _models is null || _versions is null)
            throw new InvalidOperationException("Carzin taxonomy snapshot has not been prepared.");

        var manufacturerCode = OptionalKey(source.ManufacturerCode);
        if (manufacturerCode is null) return Invalid("manufacturer_code_missing");
        if (!_manufacturers.TryGetValue(manufacturerCode, out var manufacturer)) return Invalid("manufacturer_code_unknown");

        var modelCode = OptionalKey(source.ModelCode);
        if (modelCode is null) return Invalid("model_code_missing");
        if (!_models.TryGetValue((manufacturerCode, modelCode), out var model)) return Invalid("model_code_unknown");

        var versionCode = OptionalKey(source.VersionCode);
        string? version = null;
        if (versionCode is not null && !_versions.TryGetValue((manufacturerCode, modelCode, versionCode), out version))
            return Invalid("version_code_unknown");

        return CatalogItemNormalizationResult.Valid(source with
        {
            Brand = manufacturer,
            Model = model,
            Version = version
        });
    }

    private static CatalogItemNormalizationResult Invalid(string reason) => CatalogItemNormalizationResult.Invalid(reason);
    private static string Key(string value)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Blank taxonomy code.") : value.Trim();
    private static string Name(string value)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Blank taxonomy name.") : value.Trim();
    private static string? OptionalKey(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyDictionary<TKey, string> ReadOnly<T, TKey>(
        IEnumerable<T> values, Func<T, TKey> key, Func<T, string> name) where TKey : notnull
        => new ReadOnlyDictionary<TKey, string>(values.ToDictionary(key, name));

    private static void ValidateCompleteness(CarzinVehicleCatalogTaxonomySnapshot snapshot)
    {
        if (snapshot.Manufacturers.Count == 0 || snapshot.Models.Count == 0 || snapshot.Versions.Count == 0)
            throw new InvalidOperationException("carzin_taxonomy_incomplete");
    }
}
