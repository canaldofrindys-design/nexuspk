using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Priston.Api.Health;

public static class HealthEndpoints
{
    /// <summary>
    /// /health/live: público, só diz se o processo responde.
    /// /health/ready: detalha os bancos; aceito apenas de localhost (IIS, monitoramento local).
    /// </summary>
    public static void MapPortalHealth(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("ready"),
            ResponseWriter = WriteJsonAsync,
        }).AddEndpointFilter(async (ctx, next) =>
        {
            var remote = ctx.HttpContext.Connection.RemoteIpAddress;
            return remote is not null && IPAddress.IsLoopback(remote)
                ? await next(ctx)
                : Results.NotFound();
        });
    }

    private static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), description = e.Value.Description, data = e.Value.Data }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
