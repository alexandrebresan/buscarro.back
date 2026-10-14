using Buscarro.Infrastructure.Providers;

namespace Buscarro.Tests;

public sealed class AutoDevVehicleAttributeTranslatorTests
{
    [Theory]
    [InlineData("gasoline", "Gasolina")]
    [InlineData("DÍESEL", "Diesel")]
    [InlineData("flex-fuel", "Gasolina e Álcool")]
    [InlineData("electric", "Elétrico")]
    [InlineData("hybrid", "Elétrico e Gasolina")]
    [InlineData("plug/in hybrid", "Elétrico e Gasolina")]
    [InlineData("unknown", null)]
    [InlineData("1e+3", null)]
    [InlineData(null, null)]
    public void Fuel_translation_is_canonical_and_closed(string? source, string? expected)
        => Assert.Equal(expected, AutoDevVehicleAttributeTranslator.TranslateFuel(source));

    [Theory]
    [InlineData("gasoline experimental")]
    [InlineData("hybrid prototype")]
    public void Fuel_translation_rejects_composite_unknown_values_with_recognized_tokens(string source)
        => Assert.Null(AutoDevVehicleAttributeTranslator.TranslateFuel(source));

    [Theory]
    [InlineData("Automatic CVT", "Automático CVT")]
    [InlineData("Auto-shift Manual", "Semi-automático")]
    [InlineData("Automated Manual", "Semi-automático")]
    [InlineData("Direct Drive", "Automático")]
    [InlineData("DSG Manual", "Automático / Sequencial")]
    [InlineData("lineartronic", "Automático CVT")]
    [InlineData("selectshift", "Automático / Sequencial")]
    [InlineData("manual", "Manual")]
    [InlineData("automatic", "Automático")]
    [InlineData("A", "Automático")]
    [InlineData("Type A", null)]
    [InlineData("n/a", null)]
    [InlineData("-2.5", null)]
    [InlineData(null, null)]
    public void Transmission_translation_observes_precedence(string? source, string? expected)
        => Assert.Equal(expected, AutoDevVehicleAttributeTranslator.TranslateTransmission(source));

    [Theory]
    [InlineData("ice cap", "Branco")]
    [InlineData("underground", "Cinza")]
    [InlineData("Blueprint Red", "Azul")]
    [InlineData("area-51", "Azul")]
    [InlineData("caviar", "Preto")]
    [InlineData("Platinum White", "Branco")]
    [InlineData("Black Red", "Preto")]
    [InlineData("gunmetal", "Cinza")]
    [InlineData("silver", "Prata")]
    [InlineData("blu", "Azul")]
    [InlineData("ruby", "Vermelho")]
    [InlineData("olive", "Verde")]
    [InlineData("bronze", "Bronze")]
    [InlineData("cinnamon", "Marrom")]
    [InlineData("gold", "Dourado")]
    [InlineData("sand", "Bege")]
    [InlineData("yellow", "Amarelo")]
    [InlineData("burgundy", "Vinho")]
    [InlineData("Track Bred", "Indefinida")]
    [InlineData("1e+3", "Indefinida")]
    [InlineData(null, "Indefinida")]
    public void Color_translation_uses_whole_tokens_and_declared_precedence(string? source, string expected)
        => Assert.Equal(expected, AutoDevVehicleAttributeTranslator.TranslateColor(source));
}
