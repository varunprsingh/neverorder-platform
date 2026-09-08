using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeverOrder.Application.Abstractions;
using NeverOrder.Application.Messaging;
using NeverOrder.Domain.Events;
using NeverOrder.Infrastructure.Messaging;
using NeverOrder.Infrastructure.Persistence;

namespace NeverOrder.Infrastructure.Workers;

/// <summary>
/// Drains the outbox to the broker. Until this succeeds the row stays put, so an outage delays
/// delivery rather than losing it.
/// </summary>
public sealed class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessageTransport _transport;
    private readonly IClock _clock;
    private readonly OutboxOptions _options;
    private readonly RabbitMqOptions _rabbit;
    private readonly ILogger<OutboxPublisherWorker> _logger;

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        IMessageTransport transport,
        IClock clock,
        IOptions<OutboxOptions> options,
        IOptions<RabbitMqOptions> rabbit,
        ILogger<OutboxPublisherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _transport = transport;
        _clock = clock;
        _options = options.Value;
        _rabbit = rabbit.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox publisher started; polling every {Interval}", _options.PollInterval);

        var backoff = new WorkerBackoff(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
        using var timer = new PeriodicTimer(_options.PollInterval);

        while (await SafeWaitAsync(timer, stoppingToken))
        {
            try
            {
                await DrainAsync(stoppingToken);
                backoff.RecordSuccess(_logger, "Outbox drain");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                var pause = backoff.RecordFailure(_logger, ex, "Outbox drain");

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

    private async Task DrainAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NeverOrderDbContext>();

        var now = _clock.UtcNow;

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.NextAttemptAt <= now)
            .OrderBy(m => m.OccurredAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var message in pending)
        {
            try
            {
                await _transport.PublishAsync(
                    new OutboundMessage(
                        _rabbit.Exchange,
                        EventTypes.RoutingKeyFor(message.EventType),
                        message.EventId,
                        message.EventType,
                        message.Payload),
                    ct);

                message.MarkPublished(_clock.UtcNow);
            }
            catch (Exception ex)
            {
                message.MarkFailed(ex.Message, _clock.UtcNow + _options.PublishRetryDelay);

                _logger.LogWarning(
                    ex,
                    "Could not publish outbox message {EventId} ({EventType}); attempt {Attempts}, retrying after {Delay}",
                    message.EventId, message.EventType, message.Attempts, _options.PublishRetryDelay);

                break;
            }
        }

        await db.SaveChangesAsync(ct);
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
