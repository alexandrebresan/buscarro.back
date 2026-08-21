using System.Text.Json;
using Buscarro.Api.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Buscarro.Tests;

public sealed class CatalogHealthResponseWriterTests
{
    [Fact]
    public async Task Empty_live_report_writes_bounded_json_without_entries()
    {
        var context=new DefaultHttpContext();context.Response.Body=new MemoryStream();
        var report=new HealthReport(new Dictionary<string,HealthReportEntry>(),HealthStatus.Healthy,TimeSpan.Zero);

        await CatalogHealthResponseWriter.WriteAsync(context,report);

        context.Response.Body.Position=0;
        using var json=await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("healthy",json.RootElement.GetProperty("status").GetString());
        Assert.Equal("live",json.RootElement.GetProperty("reason").GetString());
        Assert.Empty(json.RootElement.GetProperty("data").EnumerateObject());
    }
}
