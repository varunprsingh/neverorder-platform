using Microsoft.Extensions.Options;
using NeverOrder.Application.Messaging;
using RabbitMQ.Client;

namespace NeverOrder.Infrastructure.Messaging;

/// <summary>
/// Declares the full topology. Retries use TTL queues that dead-letter back into the main queue,
/// so backoff survives a worker crash and needs no broker plugin.
///
///   events(topic) --order.*--> orders --nack--> dlx --> orders.dead
///   orders.retry.Ns --ttl expiry--> requeue --> orders
/// </summary>
public sealed class RabbitMqTopology
{
    private readonly RabbitMqOptions _options;
    private readonly RetryPolicyOptions _retry;

    public RabbitMqTopology(IOptions<RabbitMqOptions> options, IOptions<RetryPolicyOptions> retry)
    {
        _options = options.Value;
        _retry = retry.Value;
    }

    /// <summary>Idempotent, so publisher and consumers can each call it before their first operation.</summary>
    public async Task DeclareAsync(IChannel channel, CancellationToken ct = default)
    {
        await channel.ExchangeDeclareAsync(_options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, arguments: null, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(_options.RetryExchange, ExchangeType.Direct, durable: true, autoDelete: false, arguments: null, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(_options.RequeueExchange, ExchangeType.Direct, durable: true, autoDelete: false, arguments: null, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(_options.DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, arguments: null, cancellationToken: ct);

        // Anything rejected without requeue (a poison message) lands straight in the dead-letter queue.
        await channel.QueueDeclareAsync(
            queue: _options.OrdersQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _options.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = _options.DeadLetterRoutingKey
            },
            cancellationToken: ct);

        await channel.QueueBindAsync(_options.OrdersQueue, _options.Exchange, _options.OrdersRoutingKey, arguments: null, cancellationToken: ct);
        await channel.QueueBindAsync(_options.OrdersQueue, _options.RequeueExchange, _options.RequeueRoutingKey, arguments: null, cancellationToken: ct);

        foreach (var delaySeconds in _retry.DeclaredDelays)
        {
            var queue = _options.RetryQueueName(delaySeconds);

            await channel.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = delaySeconds * 1000,
                    ["x-dead-letter-exchange"] = _options.RequeueExchange,
                    ["x-dead-letter-routing-key"] = _options.RequeueRoutingKey
                },
                cancellationToken: ct);

            await channel.QueueBindAsync(
                queue,
                _options.RetryExchange,
                RabbitMqOptions.RetryRoutingKey(delaySeconds),
                arguments: null,
                cancellationToken: ct);
        }

        await channel.QueueDeclareAsync(_options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: ct);
        await channel.QueueBindAsync(_options.DeadLetterQueue, _options.DeadLetterExchange, _options.DeadLetterRoutingKey, arguments: null, cancellationToken: ct);
    }
}
