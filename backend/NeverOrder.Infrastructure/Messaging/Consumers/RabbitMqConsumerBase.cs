using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeverOrder.Infrastructure.Workers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NeverOrder.Infrastructure.Messaging.Consumers;

/// <summary>Connect, declare, consume and reconnect. Subclasses only handle a delivery.</summary>
public abstract class RabbitMqConsumerBase : BackgroundService
{
    private readonly IRabbitMqConnection _connection;
    private readonly RabbitMqTopology _topology;
    private readonly ILogger _logger;

    protected RabbitMqConsumerBase(
        IRabbitMqConnection connection,
        RabbitMqTopology topology,
        RabbitMqOptions options,
        ILogger logger)
    {
        _connection = connection;
        _topology = topology;
        Options = options;
        _logger = logger;
    }

    protected RabbitMqOptions Options { get; }

    protected IChannel? Channel { get; private set; }

    protected abstract string QueueName { get; }

    protected abstract Task OnMessageAsync(BasicDeliverEventArgs delivery, byte[] body, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backoff = new WorkerBackoff(Options.ReconnectDelay, TimeSpan.FromSeconds(60));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                backoff.RecordSuccess(_logger, $"Consumer for {QueueName}");
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await SafeCloseChannelAsync();

                var pause = backoff.RecordFailure(_logger, ex, $"Consumer for {QueueName}");

                try
                {
                    await Task.Delay(pause, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        await SafeCloseChannelAsync();
    }

    private async Task StartConsumingAsync(CancellationToken ct)
    {
        var connection = await _connection.GetConnectionAsync(ct);
        Channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await _topology.DeclareAsync(Channel, ct);
        await Channel.BasicQosAsync(0, Options.PrefetchCount, global: false, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(Channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            // RabbitMQ.Client 7 may reclaim this buffer once the handler returns, so copy first.
            var body = delivery.Body.ToArray();
            await OnMessageAsync(delivery, body, CancellationToken.None);
        };

        await Channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: ct);

        _logger.LogInformation("Consuming {Queue} with prefetch {Prefetch}", QueueName, Options.PrefetchCount);
    }

    private async Task SafeCloseChannelAsync()
    {
        if (Channel is null)
        {
            return;
        }

        try
        {
            await Channel.CloseAsync();
            await Channel.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignoring error while closing the {Queue} channel", QueueName);
        }
        finally
        {
            Channel = null;
        }
    }

    protected static int ReadAttempt(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null ||
            !properties.Headers.TryGetValue(MessageHeaders.Attempt, out var raw) ||
            raw is null)
        {
            return 0;
        }

        return raw switch
        {
            int value => value,
            long value => (int)value,
            byte[] bytes when int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => 0
        };
    }

    protected static string ReadHeaderString(IReadOnlyBasicProperties properties, string key)
    {
        if (properties.Headers is null ||
            !properties.Headers.TryGetValue(key, out var raw) ||
            raw is null)
        {
            return string.Empty;
        }

        return raw switch
        {
            byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => raw.ToString() ?? string.Empty
        };
    }
}
