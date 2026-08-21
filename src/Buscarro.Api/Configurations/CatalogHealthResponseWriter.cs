using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Buscarro.Api.Configurations;

public static class CatalogHealthResponseWriter
{
    public static readonly HealthCheckOptions LiveOptions=new(){Predicate=_=>false,ResponseWriter=WriteAsync};
    public static readonly HealthCheckOptions ReadyOptions=new(){Predicate=registration=>registration.Tags.Contains("ready"),ResponseWriter=WriteAsync};
    public static async Task WriteAsync(HttpContext context,HealthReport report)
    {
        context.Response.ContentType="application/json";
        var entry=report.Entries.Values.FirstOrDefault();
        var reason=report.Entries.Count==0?"live":entry.Description??(report.Status==HealthStatus.Healthy?"ready":"database_unavailable");
        var data=report.Entries.Count==0
            ? new Dictionary<string,object>()
            : entry.Data.Where(x=>x.Key is "publicationId" or "ageSeconds" or "providerCount").ToDictionary(x=>x.Key,x=>x.Value);
        await JsonSerializer.SerializeAsync(context.Response.Body,new {status=report.Status.ToString().ToLowerInvariant(),reason,data},cancellationToken:context.RequestAborted);
    }
}
