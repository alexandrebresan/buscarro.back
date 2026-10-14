using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Buscarro.Api.Configurations;

public static class AuthProxyConfiguration
{
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        var addresses = configuration.GetSection("Auth:TrustedProxyAddresses").Get<string[]>() ?? [];
        var networks = configuration.GetSection("Auth:TrustedProxyNetworks").Get<string[]>() ?? [];
        // Empty KnownProxies/KnownNetworks would trust everyone when forwarding is enabled.
        options.ForwardedHeaders = addresses.Length + networks.Length == 0
            ? ForwardedHeaders.None : ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();
        foreach (var address in addresses)
            options.KnownProxies.Add(IPAddress.Parse(address));
        foreach (var network in networks)
        {
            var parts = network.Split('/');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var prefix))
                throw new InvalidOperationException("Auth:TrustedProxyNetworks deve conter CIDRs validos.");
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse(parts[0]), prefix));
        }
    }
}
