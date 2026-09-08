using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeverOrder.Application.Messaging;
using RabbitMQ.Client.Events;

namespace NeverOrder.Infrastructure.Messaging.Consumers;

public sealed class OrderEventsConsumer : RabbitMqConsumerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessageTransport _transport;
    private readonly RetryPolicyOptions _retry;
    private readonly ILogger<OrderEventsConsumer> _logger;

    public OrderEventsConsumer(
        IRabbitMqConnection connection,
        RabbitMqTopology topology,
        IServiceScopeFactory scopeFactory,
        IMessageTransport transport,
        IOptions<RabbitMqOptions> options,
        IOptions<RetryPolicyOptions> retry,
        ILogger<OrderEventsConsumer> logger)
        : base(connection, topology, options.Value, logger)
    {
        _scopeFactory = scopeFactory;
        _transport = transport;
        _retry = retry.Value;
        _logger = logger;
    }

    protected override string QueueName => Options.OrdersQueue;

    protected override async Task OnMessageAsync(BasicDeliverEventArgs delivery, byte[] body, CancellationToken ct)
    {
        var channel = Channel;
        if (channel is null)
        {
            return;
        }

        EventEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelope>(body, MessagingJson.Options);
        }
        catch (JsonException ex)
        {
            // Poison message: rejecting without requeue lets the queue's DLX carry it to the dead-letter
            // queue. Retrying could never help, so it does not consume an attempt.
            _logger.LogError(ex, "Unreadable message {DeliveryTag}; dead-lettering", delivery.DeliveryTag);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, ct);
            return;
        }

        if (envelope is null)
        {
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, ct);
            return;
        }

        try
        {
            // Re-establishes the originating request's id, so work done here is searchable
            // alongside the HTTP call that caused it.
            using var correlation = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = envelope.CorrelationId ?? string.Empty
            });

            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<EventDispatcher>();
            await dispatcher.DispatchAsync(envelope, ct);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex)
        {
            await RouteFailureAsync(delivery, body, envelope, ex, ct);

            // The original delivery is done with: a copy now lives in the retry or dead-letter queue.
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
    }

    private async Task RouteFailureAsync(
        BasicDeliverEventArgs delivery,
        byte[] body,
        EventEnvelope envelope,
        Exception failure,
        CancellationToken ct)
    {
        var attempt = ReadAttempt(delivery.BasicProperties) + 1;
        var delay = _retry.DelayForAttempt(attempt);

        var headers = new Dictionary<string, object?>
        {
            [MessageHeaders.Attempt] = attempt,
            [MessageHeaders.FailureReason] = Truncate(failure.Message),
            [MessageHeaders.OriginalRoutingKey] = delivery.RoutingKey
        };

        var payload = System.Text.Encoding.UTF8.GetString(body);

        if (delay is { } backoff)
        {
            var seconds = (int)backoff.TotalSeconds;

            _logger.LogWarning(
                failure,
                "Attempt {Attempt}/{MaxAttempts} failed for {EventType} {EventId}; retrying in {Delay}s",
                attempt, _retry.MaxAttempts, envelope.EventType, envelope.EventId, seconds);

            await _transport.PublishAsync(
                new OutboundMessage(
                    Options.RetryExchange,
                    RabbitMqOptions.RetryRoutingKey(seconds),
                    envelope.EventId,
                    envelope.EventType,
                    payload,
                    headers),
                ct);

            return;
        }

        _logger.LogError(
            failure,
            "Exhausted {MaxAttempts} attempts for {EventType} {EventId}; dead-lettering",
            _retry.MaxAttempts, envelope.EventType, envelope.EventId);

        await _transport.PublishAsync(
            new OutboundMessage(
                Options.DeadLetterExchange,
                Options.DeadLetterRoutingKey,
                envelope.EventId,
                envelope.EventType,
                payload,
                headers),
            ct);
    }

    private static string Truncate(string value) => value.Length > 500 ? value[..500] : value;
}
