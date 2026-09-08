using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeverOrder.Application.Abstractions;
using NeverOrder.Domain.Events;
using NeverOrder.Infrastructure.Persistence;
using RabbitMQ.Client.Events;

namespace NeverOrder.Infrastructure.Messaging.Consumers;

/// <summary>
/// Persists whatever reaches the dead-letter queue so failures are inspectable after the fact
/// rather than only visible in a log line that has already scrolled away.
/// </summary>
public sealed class DeadLetterConsumer : RabbitMqConsumerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeadLetterConsumer> _logger;

    public DeadLetterConsumer(
        IRabbitMqConnection connection,
        RabbitMqTopology topology,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<DeadLetterConsumer> logger)
        : base(connection, topology, options.Value, logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override string QueueName => Options.DeadLetterQueue;

    protected override async Task OnMessageAsync(BasicDeliverEventArgs delivery, byte[] body, CancellationToken ct)
    {
        var channel = Channel;
        if (channel is null)
        {
            return;
        }

        var payload = Encoding.UTF8.GetString(body);
        var reason = ReadHeaderString(delivery.BasicProperties, MessageHeaders.FailureReason);
        var attempts = ReadAttempt(delivery.BasicProperties);

        var eventId = Guid.Empty;
        var eventType = delivery.BasicProperties.Type ?? "Unknown";

        try
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(body, MessagingJson.Options);
            if (envelope is not null)
            {
                eventId = envelope.EventId;
                eventType = envelope.EventType;
            }
        }
        catch (JsonException)
        {
            // A malformed payload is exactly the sort of thing worth recording, so keep it raw.
            reason = string.IsNullOrEmpty(reason) ? "Message body was not valid JSON." : reason;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NeverOrderDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            db.DeadLetterEvents.Add(new DeadLetterEvent(
                eventId,
                eventType,
                payload,
                string.IsNullOrEmpty(reason) ? "Unspecified failure." : reason,
                attempts,
                clock.UtcNow));

            await db.SaveChangesAsync(ct);

            _logger.LogError(
                "Recorded dead-lettered {EventType} {EventId} after {Attempts} attempts: {Reason}",
                eventType, eventId, attempts, reason);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex)
        {
            // Requeue here: failing to record a dead letter is a storage problem worth retrying.
            _logger.LogError(ex, "Could not record dead-lettered message; requeuing");
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, ct);
        }
    }
}
