using Buscarro.Api.Controllers;
using System.Reflection;

namespace Buscarro.Tests;

public sealed class VehiclesControllerTests
{
    [Fact]
    public void VehiclesController_exposes_only_vehicle_search_endpoint()
    {
        var actionNames = typeof(VehiclesController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(["FindAll"], actionNames);
    }
}
