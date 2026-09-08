using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeverOrder.Application.Orders;
using NeverOrder.Infrastructure.Messaging;

namespace NeverOrder.Infrastructure.Workers;

/// <summary>
/// Drives the order simulation from Order.NextTransitionAt in the database rather than from
/// in-memory timers, so a restart mid-order resumes instead of stranding it.
/// </summary>
public sealed class OrderProgressionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrderSimulationOptions _simulation;
    private readonly bool _confirmOrdersToo;
    private readonly ILogger<OrderProgressionWorker> _logger;

    public OrderProgressionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OrderSimulationOptions> simulation,
        IOptions<RabbitMqOptions> rabbit,
        ILogger<OrderProgressionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _simulation = simulation.Value;
        _confirmOrdersToo = !rabbit.Value.Enabled;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Order progression worker started; polling every {Interval}", _simulation.PollInterval);

        var backoff = new WorkerBackoff(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
        using var timer = new PeriodicTimer(_simulation.PollInterval);

        while (await SafeWaitAsync(timer, stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var progression = scope.ServiceProvider.GetRequiredService<OrderProgressionService>();
                await progression.AdvanceDueOrdersAsync(_confirmOrdersToo, stoppingToken);

                backoff.RecordSuccess(_logger, "Order progression");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one bad tick kill the worker.
                var pause = backoff.RecordFailure(_logger, ex, "Order progression");

                if (!await SafeDelayAsync(pause, stoppingToken))
                {
                    break;
                }
            }
        }
    }

    private static async Task<bool> SafeDelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
