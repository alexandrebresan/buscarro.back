using Buscarro.Application.Services;

namespace Buscarro.Tests;

public sealed class VehicleSearchPromptContentAnalyzerTests
{
    private readonly VehicleSearchPromptContentAnalyzer _analyzer = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("quero um carro")]
    [InlineData("BUSCO VEÍCULOS")]
    [InlineData("gostaria de um veículo para")]
    [InlineData("preciso de carros e veículos")]
    public void HasMeaningfulTerms_returns_false_for_only_fillers(string prompt)
    {
        Assert.False(_analyzer.HasMeaningfulTerms(prompt));
    }

    [Theory]
    [InlineData("automático")]
    [InlineData("Honda")]
    [InlineData("SP")]
    [InlineData("branco")]
    [InlineData("2020")]
    [InlineData("Uno")]
    public void HasMeaningfulTerms_returns_true_for_an_objective_filter(string prompt)
    {
        Assert.True(_analyzer.HasMeaningfulTerms(prompt));
    }

    [Fact]
    public void HasMeaningfulTerms_ignores_case_and_accents_when_classifying_fillers()
    {
        Assert.False(_analyzer.HasMeaningfulTerms("QUERÍA UM VEÍCULO"));
    }
}
