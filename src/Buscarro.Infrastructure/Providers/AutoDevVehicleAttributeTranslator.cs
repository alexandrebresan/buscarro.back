using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Buscarro.Infrastructure.Providers;

internal static class AutoDevVehicleAttributeTranslator
{
    public static string? TranslateFuel(string? value)
    {
        if (MissingOrNumeric(value)) return null;
        var canonical = Canonicalize(value!);
        return canonical switch
        {
            "gasoline" => "Gasolina",
            "diesel" => "Diesel",
            "flex fuel" => "Gasolina e Álcool",
            "electric" => "Elétrico",
            "hybrid" or "plug in hybrid" => "Elétrico e Gasolina",
            _ => null
        };
    }

    public static string? TranslateTransmission(string? value)
    {
        if (MissingOrNumeric(value)) return null;
        var canonical = Canonicalize(value!);
        if (canonical == "n a") return null;
        if (Has(canonical, "auto shift manual", "automated manual")) return "Semi-automático";
        if (Has(canonical, "cvt", "ecvt", "lineartronic", "xtronic", "variable")) return "Automático CVT";
        if (Has(canonical, "dct", "dsg", "tiptronic", "shiftronic", "selectshift", "sportmatic")) return "Automático / Sequencial";
        if (Has(canonical, "manual")) return "Manual";
        if (Has(canonical, "direct drive")) return "Automático";
        if (canonical == "a" || Has(canonical, "automatic")) return "Automático";
        return null;
    }

    public static string TranslateColor(string? value)
    {
        if (MissingOrNumeric(value)) return "Indefinida";
        var canonical = Canonicalize(value!);
        if (Has(canonical, "ice cap")) return "Branco";
        if (Has(canonical, "underground")) return "Cinza";
        if (Has(canonical, "blueprint", "area 51")) return "Azul";
        if (Has(canonical, "caviar")) return "Preto";
        if (Has(canonical, "white")) return "Branco";
        if (Has(canonical, "black", "blk")) return "Preto";
        if (Has(canonical, "gray", "grey", "steel", "gunmetal")) return "Cinza";
        if (Has(canonical, "silver")) return "Prata";
        if (Has(canonical, "blue", "blu")) return "Azul";
        if (Has(canonical, "red", "crimson", "scarlet", "ruby")) return "Vermelho";
        if (Has(canonical, "green", "forest", "moss", "olive", "khaki")) return "Verde";
        if (Has(canonical, "bronze")) return "Bronze";
        if (Has(canonical, "brown", "cinnamon")) return "Marrom";
        if (Has(canonical, "gold")) return "Dourado";
        if (Has(canonical, "beige", "sand")) return "Bege";
        if (Has(canonical, "yellow")) return "Amarelo";
        if (Has(canonical, "claret", "burgundy", "currant")) return "Vinho";
        return "Indefinida";
    }

    private static bool MissingOrNumeric(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = value.Normalize(NormalizationForm.FormKC).Trim();
        return Regex.IsMatch(normalized, @"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$", RegexOptions.CultureInvariant);
    }

    private static string Canonicalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormKC).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }
        return Regex.Replace(builder.ToString().Trim(), @"\s+", " ");
    }

    private static bool Has(string canonical, params string[] patterns)
    {
        var padded = $" {canonical} ";
        return patterns.Any(pattern => padded.Contains($" {pattern} ", StringComparison.Ordinal));
    }
}
