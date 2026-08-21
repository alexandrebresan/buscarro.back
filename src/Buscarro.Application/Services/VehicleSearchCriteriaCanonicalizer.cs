using System.Globalization;
using System.Text;
using Buscarro.Application.DTOs;
using Buscarro.Application.Exceptions;

namespace Buscarro.Application.Services;

public sealed class VehicleSearchCriteriaCanonicalizer
{
    private const int MaxValues=20;
    private readonly TimeProvider _timeProvider;

    public VehicleSearchCriteriaCanonicalizer(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider=timeProvider;
    }

    public VehicleSearchCriteriaCanonicalizer():this(TimeProvider.System)
    {
    }
    public VehicleSearchCriteria Canonicalize(
        VehicleSearchCriteria source,
        VehicleSearchTaxonomy taxonomy)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(taxonomy);

        ValidateRanges(source);
        var providers=Category(source.Providers,80,"provider");
        var states=Category(source.States,80,"state");
        var cities=Category(source.Cities,120,"city");
        var manufacturers=Category(source.Manufacturers,120,"manufacturer");
        var models=Category(source.Models,120,"model");
        var versions=Category(source.Versions,160,"version");
        var colors=EnumCategory(source.Colors,taxonomy.Colors,"color");
        var gears=EnumCategory(source.Gears,taxonomy.Gears,"gear");
        var fuels=EnumCategory(source.Fuels,taxonomy.Fuels,"fuel");
        return new VehicleSearchCriteria
        {
            Query=source.Query,Order=source.Order,Page=source.Page,Results=source.Results,
            Providers=providers,States=states,Cities=cities,Manufacturers=manufacturers,Models=models,Versions=versions,Colors=colors,Gears=gears,Fuels=fuels,
            PriceMin=source.PriceMin,PriceMax=source.PriceMax,YearMin=source.YearMin,YearMax=source.YearMax
        };
    }

    private static List<string> Category(IEnumerable<string>? values, int maxLength, string name)
    {
        var raw = values?.ToArray() ?? [];
        ValidateRaw(raw, name);
        var result = new List<string>();
        foreach (var value in raw)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }
            var clean = Collapse(value);
            if (clean.Length > maxLength)
            {
                throw Invalid(
                    name,
                    "excede o limite permitido");
            }
            if (!result.Contains(
                    clean,
                    StringComparer.OrdinalIgnoreCase))
            {
                result.Add(clean);
            }
        }
        return result;
    }

    private static List<string> EnumCategory(IEnumerable<string>? values, IReadOnlyList<string> canonical, string name)
    {
        var raw = values?.ToArray() ?? [];
        ValidateRaw(raw, name);
        var result = new List<string>();
        foreach (var value in raw)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }
            var clean = Collapse(value);
            var canonicalValue =
                FindCanonical(clean, canonical, name);
            if (!result.Contains(
                    canonicalValue,
                    StringComparer.OrdinalIgnoreCase))
            {
                result.Add(canonicalValue);
            }
        }
        return result;
    }

    private static string FindCanonical(string value,IReadOnlyList<string> canonical,string name)
    {
        var normalized=Fold(Collapse(value));
        return canonical.FirstOrDefault(item=>Fold(item)==normalized)??throw Invalid(name,"possui valor nao canonico");
    }
    private static void ValidateRaw(string[] values,string name){if(values.Length>MaxValues)throw Invalid(name,"excede 20 valores brutos");}
    private static string Collapse(string value)=>string.Join(' ',value.Trim().Normalize(NormalizationForm.FormKC).Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
    private static string Fold(string value)
    {
        var decomposed=value.Normalize(NormalizationForm.FormD);var builder=new StringBuilder();
        foreach(var c in decomposed)if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)builder.Append(char.ToLowerInvariant(c));
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
    private void ValidateRanges(VehicleSearchCriteria source)
    {
        if(source.PriceMin<0||source.PriceMax<0)throw Invalid("price","nao pode ser negativo");
        if(source.PriceMin.HasValue&&source.PriceMax.HasValue&&source.PriceMin>source.PriceMax)
            throw Invalid("price","minimo nao pode exceder o maximo");
        if(source.YearMin<0||source.YearMax<0)throw Invalid("year","nao pode ser negativo");
        if(source.YearMin.HasValue&&source.YearMax.HasValue&&source.YearMin>source.YearMax)
            throw Invalid("year","minimo nao pode exceder o maximo");
        if(source.YearMax>_timeProvider.GetUtcNow().Year+1)
            throw Invalid("yearMax","nao pode exceder o proximo ano UTC");
    }
    private static ValidationException Invalid(string name,string reason)=>new($"{name} {reason}.");
}
