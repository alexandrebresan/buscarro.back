using System.Reflection;
using Buscarro.Api.Controllers;
using Buscarro.Application.DTOs;

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

    [Fact]
    public void FindAll_uses_the_repeatable_catalog_http_contract()
    {
        var parameter = typeof(VehiclesController)
            .GetMethod(nameof(VehiclesController.FindAll))!
            .GetParameters()[0];

        Assert.Equal(typeof(VehicleCatalogSearchRequest), parameter.ParameterType);
    }
}
