using Buscarro.Application.DTOs;

namespace Buscarro.Tests;

public sealed class ProviderStatusResponseTests
{
    [Fact]
    public void ProviderStatusResponse_does_not_expose_unknown_provider_totals()
    {
        var propertyNames = typeof(ProviderStatusResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("TotalResults", propertyNames);
        Assert.DoesNotContain("TotalPages", propertyNames);
    }
}
