using Buscarro.Application.Services;
using Buscarro.Application.Interfaces;

namespace Buscarro.Tests;

public sealed class VehicleSearchTaxonomyProviderTests
{
    [Fact]
    public async Task Static_taxonomy_exposes_canonical_values_without_database_access()
    {
        var providerType = typeof(SemanticProfileCatalog).Assembly.GetType(
            "Buscarro.Application.Services.StaticVehicleSearchTaxonomyProvider");
        Assert.NotNull(providerType);
        var provider = Assert.IsAssignableFrom<IVehicleSearchTaxonomyProvider>(
            Activator.CreateInstance(providerType));
        var taxonomy = await provider.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(17, taxonomy.Colors.Count);
        Assert.Equal(5, taxonomy.Gears.Count);
        Assert.Equal(10, taxonomy.Fuels.Count);
        Assert.Contains("prata", taxonomy.Colors);
        Assert.Contains("manual", taxonomy.Gears);
        Assert.Contains("gasolina e álcool", taxonomy.Fuels);
    }
}
