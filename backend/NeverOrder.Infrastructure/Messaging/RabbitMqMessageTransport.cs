using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace NeverOrder.Infrastructure.Messaging;

public sealed record OutboundMessage(
    string Exchange,
    string RoutingKey,
    Guid EventId,
    string EventType,
    string PayloadJson,
    IReadOnlyDictionary<string, object?>? Headers = null);

public interface IMessageTransport
{
    Task PublishAsync(OutboundMessage message, CancellationToken cancellationToken = default);
}

public sealed class RabbitMqMessageTransport : IMessageTransport, IAsyncDisposable
{
    private readonly SemaphoreSlim _publishGate = new(1, 1);
    private readonly IRabbitMqConnection _connection;
    private readonly RabbitMqTopology _topology;
    private readonly ILogger<RabbitMqMessageTransport> _logger;
    private IChannel? _channel;

    public RabbitMqMessageTransport(
        IRabbitMqConnection connection,
        RabbitMqTopology topology,
        ILogger<RabbitMqMessageTransport> logger)
    {
        _connection = connection;
        _topology = topology;
        _logger = logger;
    }

    public async Task PublishAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.EventId.ToString(),
            Type = message.EventType
        };

        if (message.Headers is { Count: > 0 })
        {
            properties.Headers = message.Headers.ToDictionary(h => h.Key, h => h.Value);
        }

        var body = Encoding.UTF8.GetBytes(message.PayloadJson);

        // A channel must not be used concurrently, so publishing is serialised.
        await _publishGate.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);

            await channel.BasicPublishAsync(
                exchange: message.Exchange,
                routingKey: message.RoutingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Published {EventType} {EventId} to {Exchange}/{RoutingKey}",
                message.EventType, message.EventId, message.Exchange, message.RoutingKey);
        }
        catch
        {
            // Force a fresh channel next time; the caller decides whether to retry.
            _channel = null;
            throw;
        }
        finally
        {
            _publishGate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        var connection = await _connection.GetConnectionAsync(cancellationToken);

        _channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await _topology.DeclareAsync(_channel, cancellationToken);

        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
            _channel = null;
        }

        _publishGate.Dispose();
    }
}
