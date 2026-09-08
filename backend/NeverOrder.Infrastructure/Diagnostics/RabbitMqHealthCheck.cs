using Microsoft.Extensions.Diagnostics.HealthChecks;
using NeverOrder.Infrastructure.Messaging;

namespace NeverOrder.Infrastructure.Diagnostics;

/// <summary>
/// Reuses the application's own pooled connection rather than dialling the broker separately, so a
/// health probe reports the state of the connection the workers actually publish and consume on.
/// </summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IRabbitMqConnection _connection;

    public RabbitMqHealthCheck(IRabbitMqConnection connection) => _connection = connection;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _connection.GetConnectionAsync(cancellationToken);

            return connection.IsOpen
                ? HealthCheckResult.Healthy($"Connected to {connection.Endpoint.HostName}.")
                : new HealthCheckResult(context.Registration.FailureStatus, "The connection is closed.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "The broker is unreachable.", ex);
        }
    }
}
