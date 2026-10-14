using System.Net;
using Buscarro.Api.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Buscarro.Tests;

public sealed class AuthProxyTests
{
    [Theory]
    [InlineData("10.0.0.5", true)]
    [InlineData("10.0.0.6", false)]
    public async Task Https_forwarding_only_accepts_configured_proxy(string remote, bool expectedHttps)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:TrustedProxyAddresses:0"] = "10.0.0.5"
        }).Build();
        var options = new ForwardedHeadersOptions();
        AuthProxyConfiguration.Configure(options, config);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
        await middleware.Invoke(context);
        Assert.Equal(expectedHttps, context.Request.IsHttps);
    }

    [Fact]
    public async Task Empty_proxy_configuration_does_not_trust_forwarded_headers()
    {
        var options = new ForwardedHeadersOptions();
        AuthProxyConfiguration.Configure(options, new ConfigurationBuilder().Build());
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        await new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options)).Invoke(context);
        Assert.False(context.Request.IsHttps);
    }
}
