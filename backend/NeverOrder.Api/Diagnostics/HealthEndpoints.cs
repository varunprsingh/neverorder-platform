using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NeverOrder.Infrastructure;

namespace NeverOrder.Api.Diagnostics;

public static class HealthEndpoints
{
    /// <summary>
    /// Liveness answers "is this process worth keeping?" and deliberately checks nothing else — a
    /// database outage must not make an orchestrator restart every healthy instance in a loop.
    /// Readiness answers "can this instance serve traffic?" and covers the dependencies.
    /// </summary>
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration =>
                registration.Tags.Contains(InfrastructureServiceCollectionExtensions.ReadyTag),
            ResponseWriter = WriteReportAsync
        });
    }

    private static Task WriteReportAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            durationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                // The description is ours; the exception message is not, so it never reaches the client.
                description = entry.Value.Description
            })
        }));
    }
}
